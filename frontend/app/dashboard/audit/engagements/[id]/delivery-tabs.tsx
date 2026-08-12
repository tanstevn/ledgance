"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  Download,
  Eye,
  FileCheck2,
  GitBranch,
  Link2,
  UserRound,
  FileSearch,
  FileText,
  History,
  Loader2,
  Plus,
  Search,
  Upload,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import {
  EmptyCard,
  ErrorCard,
  FieldSelect,
  FileDropZone,
  LoadingRows,
  StatusPill,
  fmtBytes,
  fmtDate,
} from "@/components/workspace";
import {
  fetchApiData,
  useApiAction,
  useApiQuery,
  useApiUpload,
} from "@/hooks/query";
import { cn } from "@/lib/utils";
import { useAuth } from "@/components/auth-context";
import { useSession } from "@/hooks/session";
import { activitySentence } from "@/lib/activity";
import {
  auditOpinions,
  evidenceCategories,
  findingSeverities,
  type ActivityRow,
  type AuditReportView,
  type EvidenceRow,
  type FindingRow,
  type OrganizationMemberRow,
  type WorkingPaperRow,
} from "@/lib/audit-types";

export function WorkingPapersTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [reference, setReference] = useState("");
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  const papers = useApiQuery<WorkingPaperRow[]>(
    `/api/audit/engagements/${engagementId}/working-papers`,
    { queryKey: ["audit-papers", engagementId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      setOpen(false);
      setReference("");
      setTitle("");
      setContent("");
      queryClient.invalidateQueries({ queryKey: ["audit-papers", engagementId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const nextAction = (paper: WorkingPaperRow) =>
    paper.status === "Draft"
      ? { action: "Prepare", label: "Prepare" }
      : paper.status === "Prepared"
        ? { action: "Review", label: "Review" }
        : paper.status === "Reviewed"
          ? { action: "Approve", label: "Approve" }
          : null;

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          New working paper
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create a working paper</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label>Reference</Label>
              <Input
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                placeholder="B-100"
              />
            </div>
            <div className="space-y-1.5 sm:col-span-2">
              <Label>Title</Label>
              <Input
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Cash lead sheet"
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Content</Label>
            <Textarea
              rows={5}
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder="Work performed, results, conclusion…"
            />
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !reference.trim() || !title.trim()}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagementId}/working-papers`,
                body: { reference: reference.trim(), title: title.trim(), content },
              })
            }
          >
            Create
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (papers.isLoading) return <LoadingRows />;
  if (papers.isError)
    return <ErrorCard errors={papers.error} onRetry={() => papers.refetch()} />;

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {papers.data && papers.data.length > 0 ? (
        <div className="space-y-3">
          {papers.data.map((paper) => {
            const next = nextAction(paper);
            return (
              <div
                key={paper.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border/60 bg-card p-4"
              >
                <div>
                  <div className="font-semibold">
                    <span className="mr-2 font-mono text-xs text-muted-foreground">
                      {paper.reference}
                    </span>
                    {paper.title}
                  </div>
                  {paper.openNotes > 0 && (
                    <div className="mt-0.5 text-xs text-amber-600 dark:text-amber-400">
                      {paper.openNotes} open review note
                      {paper.openNotes === 1 ? "" : "s"}
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <StatusPill value={paper.status} />
                  {next && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={action.isPending}
                      onClick={() =>
                        action.mutate({
                          url: `/api/audit/engagements/${engagementId}/working-papers/${paper.id}/sign-off`,
                          body: { action: next.action },
                        })
                      }
                    >
                      {next.label}
                    </Button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <EmptyCard
          icon={FileCheck2}
          title="No working papers yet"
          body="Working papers document the work performed — with preparer, reviewer and approver sign-offs enforced in order."
          action={addDialog}
        />
      )}
    </div>
  );
}

function UploadDocumentDialog({
  engagementId,
  open,
  onOpenChange,
}: {
  engagementId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const [files, setFiles] = useState<File[]>([]);
  const [category, setCategory] = useState<string>("Evidence");
  const [tags, setTags] = useState("");
  const [description, setDescription] = useState("");

  const reset = () => {
    setFiles([]);
    setCategory("Evidence");
    setTags("");
    setDescription("");
  };

  const upload = useApiUpload({
    onSuccess: () => {
      toast.success(
        files.length === 1 ? "Document uploaded." : `${files.length} documents uploaded.`,
      );
      reset();
      onOpenChange(false);
      queryClient.invalidateQueries({
        queryKey: ["audit-evidence", engagementId],
      });
    },
    onError: (errors) => {
      toast.error(errors.join(" "));
      queryClient.invalidateQueries({
        queryKey: ["audit-evidence", engagementId],
      });
    },
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next && !upload.isPending) reset();
        onOpenChange(next);
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Upload documents</DialogTitle>
          <DialogDescription>
            Re-uploading an existing file name creates a new version — the previous
            version is retained, never overwritten.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <FileDropZone
            files={files}
            onSelect={setFiles}
            disabled={upload.isPending}
          />
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="doc-category">Category</Label>
              <FieldSelect
                id="doc-category"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                disabled={upload.isPending}
              >
                {evidenceCategories.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </FieldSelect>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="doc-tags">Tags (optional)</Label>
              <Input
                id="doc-tags"
                value={tags}
                onChange={(e) => setTags(e.target.value)}
                placeholder="cash, confirmation, bank"
                disabled={upload.isPending}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="doc-description">Description (optional)</Label>
            <Input
              id="doc-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Bank confirmation — Main operating account"
              disabled={upload.isPending}
            />
          </div>
        </div>

        <DialogFooter>
          <Button
            className="font-semibold"
            disabled={files.length === 0 || upload.isPending}
            onClick={() => {
              const form = new FormData();
              files.forEach((file) => form.append("files", file));
              form.append("description", description);
              form.append("category", category);
              form.append("tags", tags);
              upload.mutate({
                url: `/api/audit/engagements/${engagementId}/evidence`,
                form,
              });
            }}
          >
            {upload.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Upload{files.length > 1 ? ` ${files.length} files` : ""}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

const categoryTones: Record<string, string> = {
  Evidence: "bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400",
  Financial: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
  Correspondence: "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400",
  Supporting: "bg-violet-100 text-violet-700 dark:bg-violet-950/40 dark:text-violet-400",
};

function CategoryBadge({ category }: { category: string }) {
  return (
    <span
      className={cn(
        "rounded-full px-2 py-0.5 text-[11px] font-medium lowercase",
        categoryTones[category] ?? "bg-muted text-muted-foreground",
      )}
    >
      {category}
    </span>
  );
}

function TagChips({ tags, limit }: { tags: string[]; limit?: number }) {
  const shown = limit ? tags.slice(0, limit) : tags;

  return (
    <div className="flex flex-wrap gap-1.5">
      {shown.map((tag) => (
        <span
          key={tag}
          className="rounded-md bg-muted px-1.5 py-0.5 text-[11px] text-muted-foreground"
        >
          {tag}
        </span>
      ))}
      {limit && tags.length > limit && (
        <span className="text-[11px] text-muted-foreground">
          +{tags.length - limit}
        </span>
      )}
    </div>
  );
}

function UploadVersionPanel({
  engagementId,
  item,
  onClose,
}: {
  engagementId: string;
  item: EvidenceRow;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [files, setFiles] = useState<File[]>([]);
  const [description, setDescription] = useState("");

  const upload = useApiUpload({
    onSuccess: () => {
      toast.success(`Version ${item.version + 1} uploaded.`);
      onClose();
      queryClient.invalidateQueries({
        queryKey: ["audit-evidence", engagementId],
      });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  return (
    <div className="flex h-full flex-col">
      <div>
        <h3 className="font-display text-base font-semibold">
          Upload a new version
        </h3>
        <p className="mt-1 text-xs text-muted-foreground">
          Becomes version {item.version + 1} of {item.fileName}. Every earlier
          version stays viewable.
        </p>
      </div>

      <div className="mt-4 space-y-4">
        <FileDropZone
          files={files}
          onSelect={setFiles}
          disabled={upload.isPending}
          single
        />
        <div className="space-y-1.5">
          <Label htmlFor="version-note">Description (optional)</Label>
          <Input
            id="version-note"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="What changed in this version"
            disabled={upload.isPending}
          />
        </div>
      </div>

      <div className="mt-auto flex justify-end gap-2 pt-4">
        <Button
          variant="ghost"
          className="font-semibold"
          disabled={upload.isPending}
          onClick={onClose}
        >
          Cancel
        </Button>
        <Button
          className="font-semibold"
          disabled={files.length === 0 || upload.isPending}
          onClick={() => {
            const form = new FormData();
            form.append("files", files[0]);
            form.append("description", description);
            form.append("supersedesEvidenceId", item.id);
            upload.mutate({
              url: `/api/audit/engagements/${engagementId}/evidence`,
              form,
            });
          }}
        >
          {upload.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Upload className="mr-2 h-4 w-4" />
          )}
          Upload version
        </Button>
      </div>
    </div>
  );
}

function DocumentDetailDialog({
  engagementId,
  item,
  memberName,
  onClose,
}: {
  engagementId: string;
  item: EvidenceRow | null;
  memberName: (userId: string) => string;
  onClose: () => void;
}) {
  const [fetching, setFetching] = useState<string | null>(null);
  const [addingVersion, setAddingVersion] = useState(false);

  const download = async (version: number, action: "view" | "download") => {
    setFetching(`${version}-${action}`);
    try {
      const url = await fetchApiData<string>(
        `/api/audit/engagements/${engagementId}/evidence/${item!.id}/download-url`,
        version === item!.version ? undefined : { version },
      );
      window.open(url, "_blank", "noopener");
    } catch (errors) {
      toast.error(
        Array.isArray(errors) ? errors.join(" ") : "The download could not be prepared.",
      );
    } finally {
      setFetching(null);
    }
  };

  const extension = item?.fileName.split(".").pop()?.toUpperCase();

  return (
    <Dialog
      open={!!item}
      onOpenChange={(next) => {
        if (!next) {
          setAddingVersion(false);
          onClose();
        }
      }}
    >
      <DialogContent
        className={cn(
          "max-h-[85vh] overflow-y-auto",
          addingVersion ? "sm:max-w-4xl" : "sm:max-w-xl",
        )}
      >
        {item && (
          <>
            <DialogHeader>
              <DialogTitle>Document details</DialogTitle>
            </DialogHeader>

            <div
              className={cn(
                "relative",
                addingVersion && "grid gap-6 lg:grid-cols-2 lg:gap-10",
              )}
            >
            <div className="space-y-4">
            <div className="flex items-start gap-3">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-muted text-primary">
                <FileText className="h-5 w-5" />
              </span>
              <div className="min-w-0">
                <h3 className="font-display text-base font-semibold leading-snug">
                  {item.fileName}
                </h3>
                <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                  <CategoryBadge category={item.category} />
                  {extension && (
                    <span className="rounded-full border border-border/60 px-2 py-0.5 text-[11px] font-medium">
                      {extension}
                    </span>
                  )}
                  <span className="rounded-full border border-border/60 px-2 py-0.5 text-[11px] font-medium">
                    {fmtBytes(item.sizeBytes)}
                  </span>
                  <span className="rounded-full border border-emerald-500/40 px-2 py-0.5 text-[11px] font-medium text-emerald-600 dark:text-emerald-400">
                    v{item.version} current
                  </span>
                </div>
                {item.tags.length > 0 && (
                  <div className="mt-2">
                    <TagChips tags={item.tags} />
                  </div>
                )}
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              {(
                [
                  ["Uploaded by", memberName(item.uploadedBy)],
                  ["Last updated", fmtDate(item.uploadedAt)],
                  [
                    "Versions",
                    `${item.versions.length} version${item.versions.length === 1 ? "" : "s"}`,
                  ],
                ] as const
              ).map(([label, value]) => (
                <div
                  key={label}
                  className="rounded-xl border border-border/60 px-3 py-2.5"
                >
                  <div className="text-xs text-muted-foreground">{label}</div>
                  <div className="mt-0.5 truncate text-sm font-semibold">
                    {value}
                  </div>
                </div>
              ))}
            </div>

            <div>
              <Separator className="mb-4" />
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h4 className="text-sm font-semibold">
                  Version history ({item.versions.length})
                </h4>
                <Button
                  size="sm"
                  variant={addingVersion ? "secondary" : "outline"}
                  className="font-semibold"
                  onClick={() => setAddingVersion((current) => !current)}
                >
                  <Upload className="mr-1.5 h-3.5 w-3.5" />
                  Upload new version
                </Button>
              </div>
              <div className="mt-3 space-y-2.5">
                {item.versions.map((version) => {
                  const current = version.version === item.version;

                  return (
                    <div
                      key={version.version}
                      className={cn(
                        "flex items-start gap-3 rounded-xl border p-3.5",
                        current ? "border-primary/40 bg-primary/5" : "border-border/60",
                      )}
                    >
                      <span
                        className={cn(
                          "flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-xs font-bold",
                          current
                            ? "bg-primary text-primary-foreground"
                            : "bg-muted text-muted-foreground",
                        )}
                      >
                        v{version.version}
                      </span>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="text-sm font-semibold">
                            Version {version.version}
                          </span>
                          {current && (
                            <span className="rounded-full bg-muted px-2 py-0.5 text-[11px] font-medium">
                              Current
                            </span>
                          )}
                        </div>
                        {version.note && (
                          <p className="mt-0.5 text-xs text-muted-foreground">
                            {version.note}
                          </p>
                        )}
                        <p className="mt-1 text-[11px] text-muted-foreground">
                          {memberName(version.uploadedBy)} ·{" "}
                          {fmtDate(version.uploadedAt)} · {fmtBytes(version.sizeBytes)}
                        </p>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        {current && (
                          <Button
                            size="icon"
                            variant="outline"
                            className="h-8 w-8 rounded-full"
                            aria-label="View the current version"
                            disabled={fetching !== null}
                            onClick={() => download(version.version, "view")}
                          >
                            {fetching === `${version.version}-view` ? (
                              <Loader2 className="h-3.5 w-3.5 animate-spin" />
                            ) : (
                              <Eye className="h-3.5 w-3.5" />
                            )}
                          </Button>
                        )}
                        <Button
                          size="sm"
                          variant={current ? "default" : "outline"}
                          className="font-semibold"
                          disabled={fetching !== null}
                          onClick={() =>
                            download(
                              version.version,
                              current ? "download" : "view",
                            )
                          }
                        >
                          {fetching ===
                          `${version.version}-${current ? "download" : "view"}` ? (
                            <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                          ) : current ? (
                            <Download className="mr-1.5 h-3.5 w-3.5" />
                          ) : (
                            <Eye className="mr-1.5 h-3.5 w-3.5" />
                          )}
                          {current ? "Download" : "View"}
                        </Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
            </div>

            {addingVersion && (
              <div className="relative lg:border-l lg:border-border/60 lg:pl-10">
                <UploadVersionPanel
                  engagementId={engagementId}
                  item={item}
                  onClose={() => setAddingVersion(false)}
                />
                <span
                  aria-hidden
                  className="absolute left-0 top-1/2 hidden h-10 w-10 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-xl border border-border/60 bg-background text-primary shadow-md lg:flex"
                >
                  <Link2 className="h-4 w-4" />
                </span>
              </div>
            )}
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

export function EvidenceTab({ engagementId }: { engagementId: string }) {
  const [uploading, setUploading] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("All");

  const evidence = useApiQuery<EvidenceRow[]>(
    `/api/audit/engagements/${engagementId}/evidence`,
    { queryKey: ["audit-evidence", engagementId] },
  );

  const members = useApiQuery<OrganizationMemberRow[]>("/api/audit/users", {
    queryKey: ["organization-members"],
  });

  const memberName = (userId: string) =>
    members.data?.find((member) => member.userId === userId)?.displayName ?? "—";

  if (evidence.isLoading) return <LoadingRows />;
  if (evidence.isError)
    return (
      <ErrorCard errors={evidence.error} onRetry={() => evidence.refetch()} />
    );

  const rows = evidence.data ?? [];
  const selected = rows.find((row) => row.id === selectedId) ?? null;
  const totalVersions = rows.reduce((sum, row) => sum + row.versions.length, 0);

  const term = search.trim().toLowerCase();
  const visible = rows
    .filter((row) => categoryFilter === "All" || row.category === categoryFilter)
    .filter(
      (row) =>
        !term ||
        row.fileName.toLowerCase().includes(term) ||
        row.description.toLowerCase().includes(term) ||
        row.tags.some((tag) => tag.includes(term)),
    );

  const uploadButton = (
    <Button className="font-semibold" onClick={() => setUploading(true)}>
      <Upload className="mr-2 h-4 w-4" />
      Upload document
    </Button>
  );

  return (
    <div className="space-y-4">
      <UploadDocumentDialog
        engagementId={engagementId}
        open={uploading}
        onOpenChange={setUploading}
      />
      <DocumentDetailDialog
        engagementId={engagementId}
        item={selected}
        memberName={memberName}
        onClose={() => setSelectedId(null)}
      />

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-lg font-bold tracking-tight">
            Documents & Evidence
          </h2>
          <p className="mt-0.5 text-xs text-muted-foreground">
            {rows.length} {rows.length === 1 ? "file" : "files"} · {totalVersions}{" "}
            total {totalVersions === 1 ? "version" : "versions"}
          </p>
        </div>
        {uploadButton}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="relative min-w-56 flex-1 sm:max-w-xs">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Search documents..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search documents"
          />
        </div>
        <div className="flex flex-wrap gap-1.5">
          {["All", ...evidenceCategories].map((value) => (
            <button
              key={value}
              type="button"
              onClick={() => setCategoryFilter(value)}
              className={cn(
                "rounded-full px-3 py-1 text-xs font-medium transition-colors",
                categoryFilter === value
                  ? "bg-primary text-primary-foreground"
                  : "bg-muted text-muted-foreground hover:text-foreground",
              )}
            >
              {value}
            </button>
          ))}
        </div>
      </div>

      {visible.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {visible.map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => setSelectedId(item.id)}
              className="flex flex-col rounded-2xl border border-border/60 bg-card p-4 text-left transition-colors hover:border-primary/40"
            >
              <div className="flex w-full items-start justify-between gap-2">
                <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-muted text-primary">
                  <FileText className="h-4 w-4" />
                </span>
                <CategoryBadge category={item.category} />
              </div>

              <h3 className="mt-3 line-clamp-2 text-sm font-semibold leading-snug">
                {item.fileName}
              </h3>
              <p className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground">
                <GitBranch className="h-3.5 w-3.5 shrink-0" />
                v{item.version} · {item.versions.length}{" "}
                {item.versions.length === 1 ? "version" : "versions"} ·{" "}
                {fmtBytes(item.sizeBytes)}
              </p>

              {item.tags.length > 0 && (
                <div className="mt-2.5">
                  <TagChips tags={item.tags} limit={3} />
                </div>
              )}

              <div className="mt-auto" aria-hidden />
              <div className="mt-3 flex w-full items-center justify-between gap-2 border-t border-border/60 pt-2.5 text-[11px] text-muted-foreground">
                <span className="flex min-w-0 items-center gap-1.5">
                  <UserRound className="h-3.5 w-3.5 shrink-0" />
                  <span className="truncate">{memberName(item.uploadedBy)}</span>
                </span>
                <span className="shrink-0">{fmtDate(item.uploadedAt)}</span>
              </div>
            </button>
          ))}
        </div>
      ) : rows.length > 0 ? (
        <EmptyCard
          icon={Search}
          title="No documents match"
          body="Try a different search term or category."
          action={
            <Button
              variant="outline"
              onClick={() => {
                setSearch("");
                setCategoryFilter("All");
              }}
            >
              Clear filters
            </Button>
          }
        />
      ) : (
        <EmptyCard
          icon={FileText}
          title="No documents yet"
          body="Every file is versioned — superseded, never overwritten — so the trail stays complete."
          action={uploadButton}
        />
      )}
    </div>
  );
}


export function FindingsTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [severity, setSeverity] = useState("Medium");
  const [recommendation, setRecommendation] = useState("");

  const findings = useApiQuery<FindingRow[]>(
    `/api/audit/engagements/${engagementId}/findings`,
    { queryKey: ["audit-findings", engagementId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      setOpen(false);
      setTitle("");
      setDescription("");
      queryClient.invalidateQueries({
        queryKey: ["audit-findings", engagementId],
      });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          Raise finding
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Raise a finding</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5 sm:col-span-2">
              <Label>Title</Label>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Severity</Label>
              <FieldSelect
                value={severity}
                onChange={(e) => setSeverity(e.target.value)}
              >
                {findingSeverities.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </FieldSelect>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Description</Label>
            <Textarea
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>Recommendation</Label>
            <Textarea
              rows={2}
              value={recommendation}
              onChange={(e) => setRecommendation(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !title.trim() || !description.trim()}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagementId}/findings`,
                body: {
                  title: title.trim(),
                  description,
                  severity,
                  recommendation,
                  evidenceIds: [],
                },
              })
            }
          >
            Raise finding
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (findings.isLoading) return <LoadingRows />;
  if (findings.isError)
    return (
      <ErrorCard errors={findings.error} onRetry={() => findings.refetch()} />
    );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {findings.data && findings.data.length > 0 ? (
        <div className="space-y-3">
          {findings.data.map((finding) => (
            <div
              key={finding.id}
              className="rounded-xl border border-border/60 bg-card p-4"
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="font-semibold">{finding.title}</div>
                <div className="flex items-center gap-2">
                  <StatusPill value={finding.severity} />
                  <StatusPill value={finding.status} />
                  {finding.status === "Open" && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={action.isPending}
                      onClick={() =>
                        action.mutate({
                          url: `/api/audit/engagements/${engagementId}/findings/${finding.id}/status`,
                          body: { action: "Resolve", note: "Resolved" },
                        })
                      }
                    >
                      Resolve
                    </Button>
                  )}
                </div>
              </div>
              <p className="mt-1.5 text-sm text-muted-foreground">
                {finding.description}
              </p>
              {finding.recommendation && (
                <p className="mt-2 text-xs text-muted-foreground">
                  <span className="font-medium text-foreground">
                    Recommendation:
                  </span>{" "}
                  {finding.recommendation}
                </p>
              )}
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={FileSearch}
          title="No findings raised"
          body="Findings capture what the audit surfaced, with severity, recommendation and resolution tracking."
          action={addDialog}
        />
      )}
    </div>
  );
}

export function ReportTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const report = useApiQuery<AuditReportView>(
    `/api/audit/engagements/${engagementId}/report`,
    { queryKey: ["audit-report", engagementId], retry: false },
  );

  const [opinion, setOpinion] = useState("Unqualified");
  const [basis, setBasis] = useState("");
  const [matters, setMatters] = useState("");

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      queryClient.invalidateQueries({ queryKey: ["audit-report", engagementId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const current = report.data;

  return (
    <div className="space-y-4">
      {current && (
        <div className="rounded-xl border border-border/60 bg-card p-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="font-semibold">
              Current draft: {current.opinion} opinion
            </div>
            {current.isFinalized ? (
              <StatusPill value="Completed" />
            ) : (
              <Button
                size="sm"
                variant="outline"
                disabled={action.isPending}
                onClick={() =>
                  action.mutate({
                    url: `/api/audit/engagements/${engagementId}/report/finalize`,
                  })
                }
              >
                Finalize report
              </Button>
            )}
          </div>
          {current.basisForOpinion && (
            <p className="mt-2 text-sm text-muted-foreground">
              {current.basisForOpinion}
            </p>
          )}
        </div>
      )}

      {!current?.isFinalized && (
        <Card className="border-border/60">
          <CardHeader className="pb-3">
            <CardTitle className="font-display text-base font-semibold">
              {current ? "Update the draft report" : "Draft the audit report"}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-1.5">
              <Label>Opinion</Label>
              <FieldSelect
                value={opinion}
                onChange={(e) => setOpinion(e.target.value)}
              >
                {auditOpinions.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </FieldSelect>
            </div>
            <div className="space-y-1.5">
              <Label>Basis for opinion</Label>
              <Textarea
                rows={3}
                value={basis}
                onChange={(e) => setBasis(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Key audit matters</Label>
              <Textarea
                rows={3}
                value={matters}
                onChange={(e) => setMatters(e.target.value)}
              />
            </div>
            <Button
              className="font-semibold"
              disabled={action.isPending || !basis.trim()}
              onClick={() =>
                action.mutate({
                  url: `/api/audit/engagements/${engagementId}/report`,
                  method: "put",
                  body: {
                    opinion,
                    basisForOpinion: basis,
                    keyAuditMatters: matters,
                    otherInformation: "",
                  },
                })
              }
            >
              Save draft
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

export function ActivityTab({ engagementId }: { engagementId: string }) {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const activity = useApiQuery<ActivityRow[]>(
    `/api/audit/engagements/${engagementId}/activity`,
    { queryKey: ["audit-activity", engagementId] },
  );

  if (activity.isLoading) return <LoadingRows />;
  if (activity.isError)
    return (
      <ErrorCard errors={activity.error} onRetry={() => activity.refetch()} />
    );

  return activity.data && activity.data.length > 0 ? (
    <div className="space-y-2">
      {activity.data.map((entry) => (
        <div
          key={entry.id}
          className="flex items-start gap-3 rounded-xl border border-border/60 bg-card px-4 py-3"
        >
          <History className="mt-0.5 h-4 w-4 flex-shrink-0 text-muted-foreground" />
          <div className="min-w-0 flex-1">
            <p className="text-sm">{activitySentence(entry, session)}</p>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {entry.actorEmail} · {new Date(entry.occurredAt).toLocaleString()}
            </p>
          </div>
        </div>
      ))}
    </div>
  ) : (
    <EmptyCard
      icon={History}
      title="No activity yet"
      body="Every material change to this engagement is recorded here, append-only."
    />
  );
}

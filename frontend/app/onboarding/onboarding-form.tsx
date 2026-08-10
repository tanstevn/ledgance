"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { ArrowRight, Building2, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "sonner";
import { useAuth } from "@/components/auth-context";
import { useApiMutation } from "@/hooks/query";
import { useSession } from "@/hooks/session";
import { isPaidPlan, type Platform } from "@/lib/plans";

interface ProvisionResult {
  organizationId: string;
}

export function OnboardingForm({
  platform,
  plan,
}: {
  platform: Platform | null;
  plan: string | null;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { user, loading: authLoading } = useAuth();
  const { data: session, isLoading: sessionLoading } = useSession(!!user);
  const [organizationName, setOrganizationName] = useState("");

  const destination =
    plan && isPaidPlan(plan)
      ? `/subscribe?plan=${plan}`
      : platform
        ? `/dashboard?platform=${platform}`
        : "/dashboard";

  useEffect(() => {
    if (!authLoading && !user) {
      router.replace("/login");
    }
  }, [authLoading, user, router]);

  useEffect(() => {
    if (session && !session.needsOnboarding) {
      router.replace(destination);
    }
  }, [session, destination, router]);

  const provision = useApiMutation<
    ProvisionResult,
    { organizationName: string; product: string | null }
  >(
    "/api/onboarding/organization",
    "post",
    {
      onSuccess: async () => {
        toast.success("Your organization is ready.");
        await queryClient.invalidateQueries({ queryKey: ["session"] });
        router.push(destination);
      },
      onError: (errors) => toast.error(errors.join(" ")),
    },
  );

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!organizationName.trim()) {
      toast.error("Please name your organization.");
      return;
    }
    provision.mutate({
      organizationName: organizationName.trim(),
      product:
        platform === "accounting"
          ? "Accounting"
          : platform === "audit"
            ? "Audit"
            : null,
    });
  };

  if (authLoading || sessionLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center p-6">
        <div className="w-full max-w-md space-y-4">
          <Skeleton className="mx-auto h-12 w-12 rounded-2xl" />
          <Skeleton className="mx-auto h-7 w-64" />
          <Skeleton className="mx-auto h-4 w-80" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      </div>
    );
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden p-6">
      <div className="pointer-events-none absolute inset-0 bg-grid opacity-[0.15]" />
      <div className="pointer-events-none absolute left-1/2 top-0 -z-10 h-[400px] w-[400px] -translate-x-1/2 rounded-full bg-primary/10 blur-[120px]" />
      <div className="relative w-full max-w-md">
        <div className="text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10">
            <Building2 className="h-6 w-6 text-primary" />
          </div>
          <h1 className="mt-6 font-display text-2xl font-bold tracking-tight">
            Set up your organization
          </h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Everything in Ledgance — clients, books, engagements, teams — lives
            inside your organization. You will be its owner.
          </p>
        </div>

        <form
          onSubmit={handleSubmit}
          className="mt-8 rounded-2xl border border-border/60 bg-card p-6"
        >
          <div className="space-y-2">
            <Label htmlFor="organization">Organization name</Label>
            <Input
              id="organization"
              type="text"
              placeholder="Avery & Partners"
              value={organizationName}
              onChange={(e) => setOrganizationName(e.target.value)}
              autoFocus
            />
            <p className="text-xs text-muted-foreground">
              Usually your firm or company name. You can invite teammates later.
            </p>
          </div>
          <Button
            type="submit"
            className="mt-6 w-full"
            disabled={provision.isPending}
          >
            {provision.isPending ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Creating organization...
              </>
            ) : (
              <>
                Create organization
                <ArrowRight className="ml-2 h-4 w-4" />
              </>
            )}
          </Button>
        </form>

        <p className="mt-6 text-center text-xs text-muted-foreground">
          Signed in as {user?.email}.{" "}
          <Link href="/" className="underline hover:text-foreground">
            Back to home
          </Link>
        </p>
      </div>
    </div>
  );
}

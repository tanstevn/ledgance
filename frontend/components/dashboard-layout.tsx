"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  BookOpen,
  Building2,
  Calculator,
  ChevronDown,
  ClipboardCheck,
  CreditCard,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  ShieldCheck,
  Sparkles,
  Sun,
  X,
} from "lucide-react";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useAuth } from "@/components/auth-context";
import { useTheme } from "@/components/theme-context";
import { isPlatformEnabled, useSession, type Session } from "@/hooks/session";
import { planPresentation } from "@/lib/plans";
import { cn } from "@/lib/utils";

/** Navigation shows only the platforms this organization has activated. */
const navSections = (session: Session | undefined) => [
  {
    title: null,
    items: [{ href: "/dashboard", label: "Overview", icon: LayoutDashboard }],
  },
  ...(isPlatformEnabled(session, "accounting")
    ? [
        {
          title: "Accounting",
          items: [
            {
              href: "/dashboard/accounting",
              label: "Entities & books",
              icon: BookOpen,
            },
            {
              href: "/dashboard/accounting/ai",
              label: "AI assistant",
              icon: Sparkles,
            },
          ],
        },
      ]
    : []),
  ...(isPlatformEnabled(session, "audit")
    ? [
        {
          title: "Audit",
          items: [
            { href: "/dashboard/audit", label: "Clients", icon: Building2 },
            {
              href: "/dashboard/audit/engagements",
              label: "Engagements",
              icon: ClipboardCheck,
            },
            {
              href: "/dashboard/audit/ai",
              label: "AI assistant",
              icon: Sparkles,
            },
          ],
        },
      ]
    : []),
  {
    title: "Organization",
    items: [
      { href: "/dashboard/billing", label: "Plans & billing", icon: CreditCard },
    ],
  },
];

export function DashboardLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, loading: authLoading, signOut } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const { data: session, isLoading: sessionLoading } = useSession(!!user);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  useEffect(() => {
    if (!authLoading && !user) {
      router.replace("/login");
    }
  }, [authLoading, user, router]);

  useEffect(() => {
    if (session?.needsOnboarding) {
      router.replace("/onboarding");
    }
  }, [session, router]);

  const isActive = (href: string) =>
    href === "/dashboard"
      ? pathname === "/dashboard"
      : pathname.startsWith(href);

  const highestPlan = session?.plans.find((plan) => plan.plan !== "Free");
  const planLabel = highestPlan
    ? `${planPresentation[highestPlan.plan]?.name ?? highestPlan.plan} · ${highestPlan.module}`
    : "Free plan";

  return (
    <div className="flex min-h-screen">
      {/* Sidebar */}
      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-50 flex w-64 flex-col border-r border-border/60 bg-card transition-transform lg:translate-x-0",
          sidebarOpen ? "translate-x-0" : "-translate-x-full",
        )}
      >
        <div className="flex h-16 items-center justify-between border-b border-border/60 px-4">
          <Link href="/dashboard" className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary">
              <ShieldCheck
                className="h-4.5 w-4.5 text-primary-foreground"
                strokeWidth={2.5}
              />
            </div>
            <span className="font-display text-base font-bold tracking-tight">
              Ledgance
            </span>
          </Link>
          <button
            onClick={() => setSidebarOpen(false)}
            className="rounded-md p-1 text-muted-foreground hover:bg-muted lg:hidden"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Organization */}
        <div className="p-3">
          <div className="flex w-full items-center gap-3 rounded-lg border border-border/60 bg-background p-2.5">
            {sessionLoading ? (
              <>
                <Skeleton className="h-8 w-8 rounded-lg" />
                <div className="flex-1 space-y-1.5">
                  <Skeleton className="h-3.5 w-28" />
                  <Skeleton className="h-3 w-20" />
                </div>
              </>
            ) : (
              <>
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-sm font-bold text-primary">
                  {(session?.organizationName ?? "?").charAt(0).toUpperCase()}
                </div>
                <div className="flex-1 overflow-hidden">
                  <div className="truncate text-sm font-semibold">
                    {session?.organizationName ?? "Your organization"}
                  </div>
                  <div className="truncate text-xs text-muted-foreground">
                    {planLabel}
                  </div>
                </div>
              </>
            )}
          </div>
        </div>

        {/* Nav */}
        <nav className="flex-1 space-y-4 overflow-y-auto px-3 py-2">
          {navSections(session).map((section) => (
            <div key={section.title ?? "root"}>
              {section.title && (
                <div className="px-3 pb-1.5 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                  {section.title}
                </div>
              )}
              <div className="space-y-1">
                {section.items.map((item) => (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={() => setSidebarOpen(false)}
                    className={cn(
                      "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors",
                      isActive(item.href)
                        ? "bg-primary/10 text-primary"
                        : "text-muted-foreground hover:bg-muted/50 hover:text-foreground",
                    )}
                  >
                    <item.icon className="h-4.5 w-4.5" />
                    {item.label}
                  </Link>
                ))}
              </div>
            </div>
          ))}
        </nav>

        {/* Dark mode toggle */}
        <div className="border-t border-border/60 p-3">
          <div className="flex items-center justify-between rounded-lg px-3 py-2">
            <div className="flex items-center gap-3 text-sm font-medium text-muted-foreground">
              {theme === "dark" ? (
                <Moon className="h-4.5 w-4.5" />
              ) : (
                <Sun className="h-4.5 w-4.5" />
              )}
              <span className="capitalize">{theme} mode</span>
            </div>
            <Switch
              checked={theme === "dark"}
              onCheckedChange={toggleTheme}
              aria-label="Toggle dark mode"
            />
          </div>
        </div>
      </aside>

      {/* Overlay for mobile */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/40 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Main content */}
      <div className="flex flex-1 flex-col lg:pl-64">
        <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-border/60 bg-background/80 px-4 backdrop-blur-xl lg:px-6">
          <div className="flex items-center gap-3">
            <button
              onClick={() => setSidebarOpen(true)}
              className="rounded-md p-1 text-muted-foreground hover:bg-muted lg:hidden"
            >
              <Menu className="h-5 w-5" />
            </button>
            {session && !sessionLoading && (
              <Badge variant="secondary" className="hidden gap-1.5 sm:flex">
                {isPlatformEnabled(session, "accounting") && (
                  <>
                    <Calculator className="h-3 w-3 text-emerald-500" />
                    {planPresentation[
                      session.plans.find((p) => p.module === "Accounting")
                        ?.plan ?? "Free"
                    ]?.name ?? "Free"}
                  </>
                )}
                {isPlatformEnabled(session, "accounting") &&
                  isPlatformEnabled(session, "audit") && (
                    <span className="text-muted-foreground">·</span>
                  )}
                {isPlatformEnabled(session, "audit") && (
                  <>
                    <ShieldCheck className="h-3 w-3 text-sky-500" />
                    {planPresentation[
                      session.plans.find((p) => p.module === "Audit")?.plan ??
                        "Free"
                    ]?.name ?? "Free"}
                  </>
                )}
              </Badge>
            )}
          </div>
          <div className="flex items-center gap-2">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button className="flex items-center gap-2 rounded-lg p-1.5 transition-colors hover:bg-muted/50">
                  <Avatar className="h-8 w-8">
                    <AvatarFallback className="bg-gradient-to-br from-emerald-500 to-sky-500 text-xs font-semibold text-white">
                      {user?.initials || "?"}
                    </AvatarFallback>
                  </Avatar>
                  <div className="hidden text-left sm:block">
                    <div className="text-sm font-semibold leading-tight">
                      {user?.name}
                    </div>
                    <div className="text-xs capitalize text-muted-foreground">
                      {session?.role || ""}
                    </div>
                  </div>
                  <ChevronDown className="hidden h-4 w-4 text-muted-foreground sm:block" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-56">
                <DropdownMenuLabel>
                  <div className="text-sm font-semibold">{user?.name}</div>
                  <div className="text-xs text-muted-foreground">
                    {user?.email}
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  className="gap-2"
                  onClick={() => router.push("/dashboard/billing")}
                >
                  <CreditCard className="h-4 w-4" />
                  Plans & billing
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  className="gap-2 text-destructive"
                  onClick={() => signOut()}
                >
                  <LogOut className="h-4 w-4" />
                  Sign out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </header>

        <main className="flex-1 p-4 lg:p-8">{children}</main>
      </div>
    </div>
  );
}

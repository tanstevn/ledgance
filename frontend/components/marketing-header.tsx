"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const navLinks = [
  { href: "/accounting", label: "Accounting" },
  { href: "/audit", label: "Audit" },
  { href: "/pricing", label: "Pricing" },
  { href: "/#ecosystem", label: "Why Ledgance" },
];

export function MarketingHeader() {
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-50 w-full border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-6">
        <Link href="/" className="flex items-center gap-2.5">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary">
            <ShieldCheck
              className="h-5 w-5 text-primary-foreground"
              strokeWidth={2.5}
            />
          </div>
          <span className="font-display text-lg font-bold tracking-tight">
            Ledgance
          </span>
        </Link>
        <nav className="hidden items-center gap-8 md:flex">
          {navLinks.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={cn(
                "text-sm font-medium transition-colors hover:text-foreground",
                pathname === link.href
                  ? "text-foreground"
                  : "text-muted-foreground",
              )}
            >
              {link.label}
            </Link>
          ))}
        </nav>
        <div className="flex items-center gap-3">
          <Link href="/login">
            <Button variant="ghost" size="sm" className="text-sm font-medium">
              Sign in
            </Button>
          </Link>
          <Link href="/#choose">
            <Button size="sm" className="text-sm font-semibold">
              Get started
            </Button>
          </Link>
        </div>
      </div>
    </header>
  );
}

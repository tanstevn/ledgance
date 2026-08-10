import Link from "next/link";
import { ShieldCheck } from "lucide-react";

const columns = [
  {
    title: "Ledgance Accounting",
    links: [
      { href: "/accounting", label: "Overview" },
      { href: "/pricing?platform=accounting", label: "Plans & pricing" },
      { href: "/signup?platform=accounting", label: "Start free" },
    ],
  },
  {
    title: "Ledgance Audit",
    links: [
      { href: "/audit", label: "Overview" },
      { href: "/pricing?platform=audit", label: "Plans & pricing" },
      { href: "/signup?platform=audit", label: "Start free" },
    ],
  },
  {
    title: "Platform",
    links: [
      { href: "/#ecosystem", label: "Why both platforms" },
      { href: "/#security", label: "Security" },
      { href: "/login", label: "Sign in" },
    ],
  },
];

export function MarketingFooter() {
  return (
    <footer className="border-t border-border/60 bg-muted/30">
      <div className="mx-auto max-w-7xl px-6 py-12">
        <div className="grid gap-8 md:grid-cols-5">
          <div className="md:col-span-2">
            <div className="flex items-center gap-2.5">
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary">
                <ShieldCheck
                  className="h-5 w-5 text-primary-foreground"
                  strokeWidth={2.5}
                />
              </div>
              <span className="font-display text-lg font-bold tracking-tight">
                Ledgance
              </span>
            </div>
            <p className="mt-4 max-w-sm text-sm leading-relaxed text-muted-foreground">
              Two professional platforms, one ecosystem: Ledgance Accounting for
              real double-entry bookkeeping and Ledgance Audit for the complete
              audit lifecycle. Use one, or connect both.
            </p>
          </div>
          {columns.map((column) => (
            <div key={column.title}>
              <h4 className="text-sm font-semibold">{column.title}</h4>
              <ul className="mt-4 space-y-3">
                {column.links.map((link) => (
                  <li key={link.label}>
                    <Link
                      href={link.href}
                      className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
        <div className="mt-12 flex flex-col items-center justify-between gap-4 border-t border-border/60 pt-8 sm:flex-row">
          <p className="text-sm text-muted-foreground">
            © 2026 Ledgance. All rights reserved.
          </p>
          <p className="text-sm text-muted-foreground">
            Built for accountants and auditors.
          </p>
        </div>
      </div>
    </footer>
  );
}

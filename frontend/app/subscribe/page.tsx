import Link from "next/link";
import { ShieldCheck } from "lucide-react";
import { SubscribeView } from "./subscribe-view";

export default async function SubscribePage({
  searchParams,
}: {
  searchParams: Promise<{ plan?: string }>;
}) {
  const params = await searchParams;

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-16 items-center border-b border-border/60 px-6">
        <Link href="/" className="flex items-center gap-2.5">
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
      </header>
      <main className="flex flex-1 items-center justify-center py-10">
        <SubscribeView planCode={params.plan ?? ""} />
      </main>
    </div>
  );
}

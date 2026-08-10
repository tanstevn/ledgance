import Link from "next/link";
import type { Metadata } from "next";
import { Calculator, ShieldCheck } from "lucide-react";
import { MarketingHeader } from "@/components/marketing-header";
import { MarketingFooter } from "@/components/marketing-footer";
import { PricingPlans } from "@/components/pricing-plans";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

export const metadata: Metadata = {
  title: "Ledgance — plans & pricing",
  description:
    "Accounting and Audit are separate products with separate plans. Start free on either; upgrade when your practice grows.",
};

export default async function PricingPage({
  searchParams,
}: {
  searchParams: Promise<{ platform?: string }>;
}) {
  const params = await searchParams;
  const initialPlatform = params.platform === "audit" ? "audit" : "accounting";

  return (
    <div className="min-h-screen bg-ambient">
      <MarketingHeader />
      <main>
        <section className="relative overflow-hidden">
          <div className="pointer-events-none absolute inset-0 bg-grid opacity-[0.15]" />
          <div className="mx-auto max-w-7xl px-6 py-16 lg:py-20">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Plans & pricing
              </Badge>
              <h1 className="font-display text-4xl font-bold tracking-tight text-balance sm:text-5xl">
                Pick a platform. Pick a plan.
              </h1>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Accounting and Audit are priced separately — subscribe to the
                one you need. Both start with a free plan that does real work.
              </p>
            </div>

            <Tabs defaultValue={initialPlatform} className="mt-12">
              <div className="flex justify-center">
                <TabsList className="h-12 rounded-full p-1">
                  <TabsTrigger
                    value="accounting"
                    className="h-10 gap-2 rounded-full px-6 text-sm font-semibold"
                  >
                    <Calculator className="h-4 w-4 text-emerald-500" />
                    Accounting
                  </TabsTrigger>
                  <TabsTrigger
                    value="audit"
                    className="h-10 gap-2 rounded-full px-6 text-sm font-semibold"
                  >
                    <ShieldCheck className="h-4 w-4 text-sky-500" />
                    Audit
                  </TabsTrigger>
                </TabsList>
              </div>

              <TabsContent value="accounting" className="mt-12">
                <PricingPlans platform="accounting" />
                <p className="mt-8 text-center text-sm text-muted-foreground">
                  New to Ledgance Accounting?{" "}
                  <Link
                    href="/accounting"
                    className="font-medium text-primary hover:underline"
                  >
                    See what it does
                  </Link>
                </p>
              </TabsContent>

              <TabsContent value="audit" className="mt-12">
                <PricingPlans platform="audit" />
                <p className="mt-8 text-center text-sm text-muted-foreground">
                  New to Ledgance Audit?{" "}
                  <Link
                    href="/audit"
                    className="font-medium text-primary hover:underline"
                  >
                    See what it does
                  </Link>
                </p>
              </TabsContent>
            </Tabs>

            <div className="mx-auto mt-16 max-w-2xl rounded-2xl border border-border/60 bg-muted/20 p-6 text-center">
              <h2 className="font-display text-lg font-semibold">
                Using both platforms?
              </h2>
              <p className="mt-2 text-sm text-muted-foreground">
                Each platform is subscribed separately — you are never required
                to buy both. Organizations on qualifying paid plans of both
                products can additionally connect them, letting Audit read the
                organization&apos;s own Accounting books with full provenance.
              </p>
            </div>
          </div>
        </section>
      </main>
      <MarketingFooter />
    </div>
  );
}

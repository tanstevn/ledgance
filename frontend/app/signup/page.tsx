import { SignupForm } from "./signup-form";
import type { Platform } from "@/lib/plans";
import { planPresentation } from "@/lib/plans";

export default async function SignUpPage({
  searchParams,
}: {
  searchParams: Promise<{ platform?: string; plan?: string }>;
}) {
  const params = await searchParams;

  const platform: Platform | null =
    params.platform === "accounting" || params.platform === "audit"
      ? params.platform
      : null;

  const plan =
    params.plan && planPresentation[params.plan] ? params.plan : null;

  return <SignupForm platform={platform} plan={plan} />;
}

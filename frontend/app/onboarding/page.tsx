import { OnboardingForm } from "./onboarding-form";
import { planPresentation, type Platform } from "@/lib/plans";

export default async function OnboardingPage({
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

  return <OnboardingForm platform={platform} plan={plan} />;
}

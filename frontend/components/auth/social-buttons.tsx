"use client";

import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { useAuth, type OAuthProvider } from "@/components/auth-context";

function GoogleIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M23.52 12.27c0-.85-.08-1.66-.22-2.45H12v4.64h6.46a5.52 5.52 0 0 1-2.4 3.62v3h3.88c2.27-2.09 3.58-5.17 3.58-8.81Z"
      />
      <path
        fill="#34A853"
        d="M12 24c3.24 0 5.96-1.07 7.94-2.91l-3.88-3c-1.07.72-2.45 1.15-4.06 1.15-3.13 0-5.78-2.11-6.72-4.95H1.27v3.1A12 12 0 0 0 12 24Z"
      />
      <path
        fill="#FBBC05"
        d="M5.28 14.29a7.21 7.21 0 0 1 0-4.58v-3.1H1.27a12 12 0 0 0 0 10.78l4.01-3.1Z"
      />
      <path
        fill="#EA4335"
        d="M12 4.77c1.76 0 3.34.6 4.59 1.79l3.44-3.44A11.98 11.98 0 0 0 12 0 12 12 0 0 0 1.27 6.61l4.01 3.1C6.22 6.87 8.87 4.77 12 4.77Z"
      />
    </svg>
  );
}

function LinkedInIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" aria-hidden="true">
      <path
        fill="#0A66C2"
        d="M20.45 20.45h-3.55v-5.57c0-1.33-.03-3.04-1.85-3.04-1.86 0-2.14 1.45-2.14 2.94v5.67H9.35V9h3.41v1.56h.05a3.74 3.74 0 0 1 3.37-1.85c3.6 0 4.27 2.37 4.27 5.46v6.28ZM5.34 7.43a2.06 2.06 0 1 1 0-4.12 2.06 2.06 0 0 1 0 4.12ZM7.12 20.45H3.56V9h3.56v11.45ZM22.22 0H1.77C.79 0 0 .77 0 1.73v20.54C0 23.23.79 24 1.77 24h20.45c.98 0 1.78-.77 1.78-1.73V1.73C24 .77 23.2 0 22.22 0Z"
      />
    </svg>
  );
}

/**
 * Icon-only OAuth entry points. The redirect completes back at `redirectTo`, where the
 * Supabase client picks the session out of the URL. Providers must be enabled in the
 * Supabase dashboard — a disabled provider surfaces as the error toast, never a dead
 * button. Accessible names live in aria-label/title since the buttons carry no text.
 */
export function SocialAuthButtons({
  redirectTo,
  action = "Continue",
}: {
  redirectTo: string;
  action?: string;
}) {
  const { signInWithOAuth } = useAuth();
  const [pending, setPending] = useState<OAuthProvider | null>(null);

  const start = async (provider: OAuthProvider) => {
    setPending(provider);
    try {
      await signInWithOAuth(
        provider,
        `${window.location.origin}${redirectTo}`,
      );
      // The browser navigates away on success; resetting is for the error path.
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : "Could not start the sign-in. Please try again.",
      );
      setPending(null);
    }
  };

  const providers: {
    provider: OAuthProvider;
    name: string;
    icon: React.ReactNode;
  }[] = [
    { provider: "google", name: "Google", icon: <GoogleIcon /> },
    { provider: "linkedin_oidc", name: "LinkedIn", icon: <LinkedInIcon /> },
  ];

  return (
    <div className="flex justify-center gap-3">
      {providers.map(({ provider, name, icon }) => (
        <Button
          key={provider}
          type="button"
          variant="outline"
          aria-label={`${action} with ${name}`}
          title={`${action} with ${name}`}
          className="h-11 flex-1"
          disabled={pending !== null}
          onClick={() => start(provider)}
        >
          {pending === provider ? (
            <Loader2 className="h-5 w-5 animate-spin" />
          ) : (
            icon
          )}
        </Button>
      ))}
    </div>
  );
}

export function OrDivider() {
  return (
    <div className="flex items-center gap-3">
      <span className="h-px flex-1 bg-border" />
      <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        or continue with email
      </span>
      <span className="h-px flex-1 bg-border" />
    </div>
  );
}

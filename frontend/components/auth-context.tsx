"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useRouter } from "next/navigation";
import type { Session, User } from "@supabase/supabase-js";
import { getSupabaseClient } from "@/lib/supabase";

interface AuthUser {
  id: string;
  name: string;
  email: string;
  initials: string;
  role: string;
}

export type OAuthProvider = "google" | "linkedin_oidc";

interface AuthContextValue {
  user: AuthUser | null;
  accessToken: string | null;
  loading: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  signUp: (name: string, email: string, password: string) => Promise<void>;
  signInWithOAuth: (provider: OAuthProvider, redirectTo: string) => Promise<void>;
  signOut: () => Promise<void>;
  resetPassword: (email: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function initialsOf(name: string) {
  return (
    name
      .split(" ")
      .filter(Boolean)
      .map((part) => part[0])
      .join("")
      .slice(0, 2)
      .toUpperCase() || "?"
  );
}

function toAuthUser(user: User): AuthUser {
  const name =
    (user.user_metadata?.full_name as string | undefined) ??
    user.email?.split("@")[0] ??
    "";

  return {
    id: user.id,
    name,
    email: user.email ?? "",
    initials: initialsOf(name),
    // Organization role is authoritative on the server and is read from /api/session.
    role: (user.app_metadata?.org_role as string | undefined) ?? "member",
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  const router = useRouter();

  useEffect(() => {
    const supabase = getSupabaseClient();

    supabase.auth.getSession().then(({ data }) => {
      setSession(data.session);
      setLoading(false);
    });

    const { data: subscription } = supabase.auth.onAuthStateChange(
      (_event, nextSession) => setSession(nextSession),
    );

    return () => subscription.subscription.unsubscribe();
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const { error } = await getSupabaseClient().auth.signInWithPassword({
      email,
      password,
    });

    if (error) throw new Error(error.message);
  }, []);

  const signUp = useCallback(
    async (name: string, email: string, password: string) => {
      const { error } = await getSupabaseClient().auth.signUp({
        email,
        password,
        options: { data: { full_name: name } },
      });

      if (error) throw new Error(error.message);
    },
    [],
  );

  const signInWithOAuth = useCallback(
    async (provider: OAuthProvider, redirectTo: string) => {
      const { error } = await getSupabaseClient().auth.signInWithOAuth({
        provider,
        options: { redirectTo },
      });

      if (error) throw new Error(error.message);
    },
    [],
  );

  const signOut = useCallback(async () => {
    await getSupabaseClient().auth.signOut();
    router.push("/");
  }, [router]);

  const resetPassword = useCallback(async (email: string) => {
    const { error } = await getSupabaseClient().auth.resetPasswordForEmail(
      email,
      { redirectTo: `${window.location.origin}/login` },
    );

    if (error) throw new Error(error.message);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ? toAuthUser(session.user) : null,
      accessToken: session?.access_token ?? null,
      loading,
      signIn,
      signUp,
      signInWithOAuth,
      signOut,
      resetPassword,
    }),
    [session, loading, signIn, signUp, signInWithOAuth, signOut, resetPassword],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

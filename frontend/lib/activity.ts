import type { Session } from "@/hooks/session";

export interface ActivityEntryLike {
  summary: string;
  actorUserId: string;
  actorEmail: string;
  occurredAt: string;
}

/**
 * The server records what someone did as an active-voice predicate ("approved the audit plan
 * for FY2026 Financial Statement Audit."), so a sentence is the actor followed by the record
 * itself. Nothing is reworded here — the audit trail says what it says.
 */
export const activityActor = (
  entry: ActivityEntryLike,
  session: Session | undefined,
): string =>
  session && entry.actorUserId === session.userId
    ? "You"
    : humanName(entry.actorEmail);

export const activitySentence = (
  entry: ActivityEntryLike,
  session: Session | undefined,
): string => `${activityActor(entry, session)} ${entry.summary}`;

/** Falls back to the readable part of an email when we have no display name. */
const humanName = (email: string): string => {
  const local = email.split("@")[0];

  if (!local) {
    return "Someone";
  }

  return local
    .replace(/[._-]+/g, " ")
    .replace(/\b\w/g, (character) => character.toUpperCase());
};

export const activityInitials = (
  entry: ActivityEntryLike,
  session: Session | undefined,
): string =>
  session && entry.actorUserId === session.userId
    ? "You"
    : (entry.actorEmail[0] ?? "?").toUpperCase();

/** "3h ago" style, falling back to a date once an entry is older than a week. */
export const relativeTime = (value: string): string => {
  const occurred = new Date(value).getTime();
  const minutes = Math.floor((Date.now() - occurred) / 60000);

  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes}m ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;

  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;

  return new Date(value).toLocaleDateString();
};

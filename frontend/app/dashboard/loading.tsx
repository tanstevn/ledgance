import { Skeleton } from "@/components/ui/skeleton";

/**
 * Suspense fallback for every dashboard route. Without this file the App Router keeps the
 * previous page on screen until the next segment is ready, which reads as the click doing
 * nothing; with it, navigation swaps immediately — sidebar stays put, the content area
 * shows this sketch, and the page's own data skeletons take over on mount.
 */
export default function DashboardLoading() {
  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-2">
          <Skeleton className="h-7 w-44" />
          <Skeleton className="h-4 w-72" />
        </div>
        <Skeleton className="h-10 w-32 rounded-md" />
      </div>

      <Skeleton className="h-10 w-full max-w-sm rounded-md" />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {Array.from({ length: 8 }).map((_, index) => (
          <Skeleton key={index} className="h-56 w-full rounded-2xl" />
        ))}
      </div>
    </div>
  );
}

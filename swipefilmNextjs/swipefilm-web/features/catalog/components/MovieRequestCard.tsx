"use client";

import Image from "next/image";
import Link from "next/link";
import { Clapperboard, Calendar, Check, X, Plus } from "lucide-react";
import { cn, tmdbImage } from "@/lib/utils";
import { statusFromCode, requestMedia } from "@/features/catalog/api";
import type { MovieRequest } from "@/features/catalog/api";
import { getStatusConfig, RequestStatusBadge } from "@/components/ui/Badges";
import { IconButton, Button } from "@/components/ui/Button";

interface MovieRequestCardProps {
  request: MovieRequest;
  isManager?: boolean;
  forcedVariant?: "available" | "pending" | "downloading";
  onApprove?: (id: string) => void;
  onDecline?: (id: string) => void;
  onCancel?: (id: string) => void;
  onRequest?: () => void;
  requesting?: boolean;
  className?: string;
}

// ── Card ─────────────────────────────────────────────────────────

export function MovieRequestCard({
  request,
  isManager = false,
  forcedVariant,
  onApprove,
  onDecline,
  onCancel,
  onRequest,
  requesting = false,
  className,
}: MovieRequestCardProps) {
  const media = requestMedia(request);
  const poster = tmdbImage(media.posterPath, "w185");
  const backdrop = tmdbImage(media.posterPath, "w500");
  const status = getStatusConfig(request.status, forcedVariant);
  const isPending =
    forcedVariant === "pending" || statusFromCode(request.status) === "pending";

  const requestedDate = new Date(request.requestedAt).toLocaleDateString(
    "fr-FR",
    {
      day: "numeric",
      month: "short",
      year: "numeric",
    },
  );

  const requester = request.requestedBy;
  const href =
    media.contentType === "series" ? `/series/${media.id}` : `/movie/${media.id}`;

  return (
    <div
      className={cn(
        "border-border bg-surface relative flex overflow-hidden rounded-xl border",
        "min-h-[200px]",
        className,
      )}
    >
      {/* ── Backdrop flouté ── */}
      {backdrop && (
        <div className="pointer-events-none absolute inset-0 overflow-hidden">
          <Image
            src={backdrop}
            alt=""
            fill
            sizes="100vw"
            className="scale-110 object-cover opacity-[0.56] blur-sm"
            aria-hidden
          />
          <div className="from-surface via-surface/95 to-surface/80 absolute inset-0 bg-gradient-to-r" />
        </div>
      )}

      {/* ── Bande de statut (gauche) ── */}
      <div className={cn("w-1 flex-shrink-0 self-stretch", status.stripe)} />

      {/* ── Poster ── */}
      <Link
        href={href}
        className="bg-surface-alt border-border/60 relative my-3 mr-0 ml-3 aspect-[2/3] h-50 flex-shrink-0 overflow-hidden rounded-lg border shadow-md"
      >
        {poster ? (
          <Image
            src={poster}
            alt={media.title}
            fill
            sizes="152px"
            className="object-cover"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center">
            <Clapperboard className="size-5 text-text-secondary" />
          </div>
        )}
      </Link>

      {/* ── Contenu ── */}
      <div className="relative flex min-w-0 flex-1 flex-col justify-start gap-1.5 px-4 py-3">
        {/* Titre */}
        <Link href={href} className="hover:text-accent w-fit">
          <p className="text-text-primary truncate text-sm leading-tight font-semibold sm:text-base">
            {media.title}
          </p>
        </Link>

        {/* Meta : type + date */}
        <div className="text-text-secondary flex flex-wrap items-center gap-2 text-xs">
          <span className="bg-surface-alt border-border/60 rounded border px-1.5 py-0.5 text-[11px] font-medium capitalize">
            {media.contentType === "movie" ? "Film" : "Série"}
          </span>
          <span className="flex items-center gap-1">
            <Calendar className="size-4" />
            {requestedDate}
          </span>
        </div>

        {/* Demandé par */}
        {requester?.displayName && (
          <div className="text-text-secondary flex items-center gap-1.5 text-xs">
            <div className="bg-accent/20 border-accent/30 flex h-4 w-4 flex-shrink-0 items-center justify-center rounded-full border">
              {requester.avatarUrl ? (
                <Image
                  src={requester.avatarUrl}
                  alt={requester.displayName}
                  width={16}
                  height={16}
                  className="rounded-full object-cover"
                />
              ) : (
                <span className="text-accent text-[8px] font-bold">
                  {requester.displayName[0].toUpperCase()}
                </span>
              )}
            </div>
            <span>{requester.displayName}</span>
          </div>
        )}
      </div>

      {/* ── Actions (droite) ── */}
      <div className="relative flex flex-shrink-0 flex-col items-end justify-between gap-2 px-4 py-3">
        {/* Badge de statut */}
        <RequestStatusBadge
          statusCode={request.status}
          forced={forcedVariant}
        />

        {/* Boutons d'action */}
        <div className="flex items-center gap-1.5">
          {isPending && isManager && (
            <>
              <IconButton
                variant="success"
                className="rounded-lg"
                onClick={() => onApprove?.(request.id)}
                title="Approuver"
              >
                <Check className="size-4" />
              </IconButton>
              <IconButton
                variant="danger"
                className="rounded-lg"
                onClick={() => onDecline?.(request.id)}
                title="Refuser"
              >
                <X className="size-4" />
              </IconButton>
            </>
          )}

          {isPending && !isManager && onCancel && (
            <IconButton
              variant="neutral"
              className="rounded-lg"
              onClick={() => onCancel(request.id)}
              title="Annuler"
            >
              <X className="size-4" />
            </IconButton>
          )}

          {onRequest && !isPending && (
            <Button
              variant="accent-soft"
              size="sm"
              className="rounded-lg px-3 py-1.5 text-xs"
              onClick={onRequest}
              disabled={requesting}
            >
              {requesting ? (
                <span className="h-3 w-3 animate-spin rounded-full border-2 border-current border-t-transparent" />
              ) : (
                <Plus className="size-4" />
              )}
              {requesting ? "Envoi…" : "Requêter"}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

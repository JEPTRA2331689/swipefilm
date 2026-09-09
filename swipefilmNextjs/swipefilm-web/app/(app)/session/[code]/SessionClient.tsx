"use client";

import { useEffect, useState } from "react";
import { useStaticRouteId } from "@/hooks/useStaticRouteId";
import { useAuth } from "@/features/auth/AuthContext";
import { TopNav } from "@/components/layout/TopNav";
import { Button } from "@/components/ui/Button";
import {
  getSessionState,
  getSessionMatches,
  revealSessionMatches,
  type SessionState,
  type SessionMatchResult,
} from "@/features/session/api";
import { useSessionHub } from "@/features/session/useSessionHub";
import { SessionLobby } from "@/features/session/components/SessionLobby";
import { HostGenrePicker } from "@/features/session/components/HostGenrePicker";
import { SessionSwipeDeck } from "@/features/session/components/SessionSwipeDeck";
import { MatchesReveal } from "@/features/session/components/MatchesReveal";

export default function SessionClient() {
  const code = useStaticRouteId();
  const { user, loading: authLoading } = useAuth();
  const [state, setState] = useState<SessionState | null>(null);
  const [matches, setMatches] = useState<SessionMatchResult[] | null>(null);
  const [revealing, setRevealing] = useState(false);
  const [notFound, setNotFound] = useState(false);

  const { state: hubState, matches: hubMatches, swipeProgress } = useSessionHub(code);

  useEffect(() => {
    if (!code) return;
    getSessionState(code)
      .then((s) => {
        setState(s);
        // ✅ Rouvrir une session déjà terminée (fenêtre de grâce de 2h) doit
        // remontrer les matchs — sinon "matches" reste null localement et
        // MatchesReveal affiche à tort "aucun consensus".
        if (s.status === 1) getSessionMatches(code).then(setMatches);
      })
      .catch(() => setNotFound(true));
  }, [code]);

  useEffect(() => {
    if (hubState) setState(hubState);
  }, [hubState]);

  useEffect(() => {
    if (hubMatches) setMatches(hubMatches);
  }, [hubMatches]);

  const isHost = !!user && !!state && user.id === state.hostUserId;

  async function handleReveal() {
    if (!state) return;
    setRevealing(true);
    try {
      setMatches(await revealSessionMatches(state.code));
    } finally {
      setRevealing(false);
    }
  }

  if (notFound) {
    return (
      <div className="flex h-screen items-center justify-center px-4">
        <p className="text-text-secondary text-sm">
          Session introuvable ou expirée.
        </p>
      </div>
    );
  }

  if (authLoading || !code || !state) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
      </div>
    );
  }

  const phase: "lobby" | "setup" | "swiping" | "results" =
    matches !== null || state.status === 1
      ? "results"
      : state.hasStarted
        ? "swiping"
        : state.hasLaunched
          ? "setup"
          : "lobby";

  return (
    // ✅ h-screen + overflow-hidden (pas min-h-screen) — SwipeFocusRail a
    // besoin d'une hauteur définie sur toute la chaîne flex-1/h-full pour
    // s'afficher ; avec min-h-screen le deck restait présent dans le DOM
    // (get_page_text le voyait) mais visuellement réduit à 0px de haut.
    <div className="flex h-screen flex-col overflow-hidden">
      <TopNav />

      {/* ✅ Barre condensée code+membres — masquée en lobby, SessionLobby
      affiche déjà le code (via le QR) et la liste des participants en plus
      grand ; la dupliquer ici serait redondant. */}
      {phase !== "lobby" && (
        <div className="border-border flex flex-shrink-0 flex-wrap items-center gap-2 border-b px-4 py-3 md:px-12">
          <span className="text-text-secondary text-xs">Code {state.code} ·</span>
          {state.members.map((m) => {
            const count = swipeProgress[m.userId] ?? m.swipeCount;
            return (
              <span
                key={m.userId}
                className="bg-surface-alt text-text-secondary rounded-pill px-2.5 py-1 text-xs"
              >
                {m.displayName}
                {m.isHost && " 👑"}
                {phase === "swiping" && ` · ${count}`}
              </span>
            );
          })}
        </div>
      )}

      {phase === "lobby" && (
        <div className="min-h-0 flex-1 overflow-y-auto">
          <SessionLobby
            code={state.code}
            members={state.members}
            isHost={isHost}
            onLaunched={() =>
              setState((s) => (s ? { ...s, hasLaunched: true } : s))
            }
          />
        </div>
      )}

      {phase === "setup" && (
        <div className="flex min-h-0 flex-1 flex-col items-center justify-center overflow-y-auto px-4 py-8">
          {isHost ? (
            <HostGenrePicker
              code={state.code}
              onStarted={() =>
                setState((s) => (s ? { ...s, hasStarted: true } : s))
              }
            />
          ) : (
            <p className="text-text-secondary py-8 text-center text-sm">
              En attente que l&apos;hôte configure la session…
            </p>
          )}
        </div>
      )}

      {phase === "swiping" && (
        <>
          <SessionSwipeDeck code={state.code} />
          {isHost && (
            <div className="border-border flex flex-shrink-0 justify-center border-t px-4 py-3">
              <Button variant="primary" onClick={handleReveal} disabled={revealing}>
                {revealing ? "Calcul…" : "Voir les résultats"}
              </Button>
            </div>
          )}
        </>
      )}

      {phase === "results" && (
        <div className="min-h-0 flex-1 overflow-y-auto">
          <MatchesReveal matches={matches ?? []} />
        </div>
      )}
    </div>
  );
}

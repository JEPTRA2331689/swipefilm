"use client";

import { useEffect, useState } from "react";
import { LogIn, UserRound } from "lucide-react";
import { useAuth } from "@/features/auth/AuthContext";
import { AuthForm } from "@/features/auth/components/AuthForm";
import { Button } from "@/components/ui/Button";
import { getSessionState, joinSession, type SessionState } from "@/features/session/api";

type Choice = "none" | "login" | "guest";

export function JoinScreen({ code }: { code: string }) {
  const { user, loading: authLoading, login } = useAuth();
  const [session, setSession] = useState<SessionState | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [choice, setChoice] = useState<Choice>("none");
  const [guestName, setGuestName] = useState("");
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getSessionState(code)
      .then(setSession)
      .catch(() => setNotFound(true));
  }, [code]);

  // ✅ Navigation dure — force AuthProvider (monté une fois à la racine) à
  // relire /api/auth/me, indispensable juste après la pose d'un tout nouveau
  // cookie invité qu'il ne connaît pas encore.
  function goToSession() {
    window.location.href = `/session/${code}`;
  }

  async function handleJoinConnected() {
    setJoining(true);
    setError(null);
    try {
      await joinSession(code);
      goToSession();
    } catch {
      setError("Impossible de rejoindre cette session.");
      setJoining(false);
    }
  }

  async function handleLoginThenJoin(values: Record<string, string>) {
    await login(values.username, values.password);
    await joinSession(code);
    goToSession();
  }

  async function handleGuestJoin(e: React.FormEvent) {
    e.preventDefault();
    if (!guestName.trim()) return;
    setJoining(true);
    setError(null);
    try {
      await joinSession(code, guestName.trim());
      goToSession();
    } catch {
      setError("Impossible de rejoindre cette session.");
      setJoining(false);
    }
  }

  if (notFound) {
    return (
      <div className="flex min-h-screen items-center justify-center px-4">
        <p className="text-text-secondary text-sm">
          Session introuvable ou expirée — demande un nouveau code à l&apos;hôte.
        </p>
      </div>
    );
  }

  if (authLoading || !session) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
      </div>
    );
  }

  // ✅ status !== 0 (Active) — la session est dans sa fenêtre de consultation
  // de 2h post-fin (GetViewableSessionByCodeAsync) : l'état reste lisible
  // mais on ne peut plus la rejoindre pour swiper.
  if (session.status !== 0) {
    return (
      <div className="flex min-h-screen items-center justify-center px-4">
        <p className="text-text-secondary text-center text-sm">
          Cette session est terminée — demande à l&apos;hôte d&apos;en créer une
          nouvelle.
        </p>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-12">
      <div className="rounded-card border-border bg-surface/70 w-full max-w-md border p-8 shadow-2xl backdrop-blur-xl">
        <div className="mb-6 text-center">
          <span className="font-display text-text-primary text-2xl font-bold tracking-tight">
            Swipe<span className="text-accent">Film</span>
          </span>
        </div>

        <h1 className="font-display text-text-primary text-center text-xl font-semibold">
          Rejoindre la session
        </h1>
        <p className="text-text-secondary mt-1 text-center text-sm">
          Code {session.code} — {session.members.length} participant
          {session.members.length !== 1 ? "s" : ""}
        </p>

        {error && (
          <p className="bg-error/10 border-error/30 text-error mt-4 rounded-md border px-3 py-2 text-xs">
            {error}
          </p>
        )}

        {user ? (
          <Button
            variant="primary"
            className="mt-6 w-full"
            onClick={handleJoinConnected}
            disabled={joining}
          >
            {joining ? "Connexion…" : `Rejoindre en tant que ${user.displayName}`}
          </Button>
        ) : choice === "none" ? (
          <div className="mt-6 space-y-3">
            <Button
              variant="outline"
              className="w-full gap-2"
              onClick={() => setChoice("login")}
            >
              <LogIn className="size-4" aria-hidden /> Se connecter
            </Button>
            <Button
              variant="primary"
              className="w-full gap-2"
              onClick={() => setChoice("guest")}
            >
              <UserRound className="size-4" aria-hidden /> Continuer en invité
            </Button>
          </div>
        ) : choice === "login" ? (
          <div className="mt-6">
            <AuthForm
              bare
              title=""
              subtitle=""
              fields={[
                { name: "username", label: "Nom d'utilisateur", type: "text", placeholder: "votre_nom_utilisateur" },
                { name: "password", label: "Mot de passe", type: "password", placeholder: "••••••••" },
              ]}
              submitLabel="Se connecter et rejoindre"
              onSubmit={handleLoginThenJoin}
            />
          </div>
        ) : (
          <form onSubmit={handleGuestJoin} className="mt-6 space-y-4">
            <div>
              <label className="text-text-secondary mb-1.5 block text-xs font-medium">
                Quel est ton prénom ?
              </label>
              <input
                type="text"
                value={guestName}
                onChange={(e) => setGuestName(e.target.value)}
                placeholder="Ton prénom"
                autoFocus
                required
                className="rounded-input border-border bg-surface-alt text-text-primary placeholder:text-text-secondary/50 focus:border-accent w-full border px-3.5 py-2.5 text-sm transition-colors outline-none"
              />
            </div>
            <Button type="submit" variant="primary" className="w-full" disabled={joining}>
              {joining ? "Connexion…" : "Rejoindre"}
            </Button>
          </form>
        )}
      </div>
    </div>
  );
}

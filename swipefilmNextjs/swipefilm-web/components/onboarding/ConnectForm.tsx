"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { cn } from "@/lib/utils";

interface ServerOption {
  id: string;
  label: string;
}

type AuthMode = "credentials" | "apikey";

interface ConnectFormProps {
  title: string;
  subtitle?: string;
  options: ServerOption[];
  onSubmit: (data: {
    type: string;
    friendlyName: string;
    url: string;
    apiKey?: string;
    username?: string;
    password?: string;
  }) => Promise<void>;
  onBack?: () => void;
  onSkip?: () => void;
  submitLabel?: string;
  skipLabel?: string;
}

export function ConnectForm({
  title,
  subtitle,
  options,
  onSubmit,
  onBack,
  onSkip,
  submitLabel = "Connecter",
  skipLabel,
}: ConnectFormProps) {
  const [selected, setSelected] = useState(options[0]?.id ?? "");
  const [authMode, setAuthMode] = useState<AuthMode>("apikey");
  const [friendlyName, setFriendlyName] = useState("");
  const [url, setUrl] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!url.trim()) {
      setError("Renseigne l'adresse du serveur.");
      return;
    }
    if (authMode === "apikey" && !apiKey.trim()) {
      setError("Renseigne la clé API.");
      return;
    }
    if (authMode === "credentials" && (!username.trim() || !password.trim())) {
      setError("Renseigne le nom d'utilisateur et le mot de passe.");
      return;
    }

    setLoading(true);
    try {
      await onSubmit({
        type: selected,
        friendlyName: friendlyName.trim() || `Mon ${selected}`,
        url: url.trim(),
        apiKey: authMode === "apikey" ? apiKey.trim() : undefined,
        username: authMode === "credentials" ? username.trim() : undefined,
        password: authMode === "credentials" ? password.trim() : undefined,
      });
    } catch {
      setError(
        "La connexion a échoué — vérifie l'adresse et les identifiants.",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="rounded-card border-border bg-surface/70 relative z-10 w-full max-w-md border p-8 shadow-2xl backdrop-blur-xl">
      <div className="mb-4 text-center">
        <span className="font-display text-text-primary text-xl font-bold">
          Swipe<span className="text-accent">Film</span>
        </span>
      </div>

      <h1 className="font-display text-text-primary text-center text-xl font-semibold">
        {title}
      </h1>
      {subtitle && (
        <p className="text-text-secondary mt-1.5 text-center text-sm">
          {subtitle}
        </p>
      )}

      {/* Toggle type serveur */}
      {options.length > 1 && (
        <div className="mt-5 flex justify-center gap-2">
          {options.map((opt) => (
            <button
              key={opt.id}
              type="button"
              onClick={() => setSelected(opt.id)}
              className={cn(
                "rounded-pill border px-4 py-1.5 text-[13px] font-medium transition-colors",
                selected === opt.id
                  ? "border-accent bg-accent/20 text-accent"
                  : "border-border text-text-secondary hover:text-text-primary",
              )}
            >
              {opt.label}
            </button>
          ))}
        </div>
      )}

      {/* Toggle mode d'authentification */}
      <div className="rounded-pill bg-surface-alt mt-4 flex justify-center gap-1 p-1">
        {(
          [
            { id: "apikey", label: "Clé API" },
            { id: "credentials", label: "Identifiants" },
          ] as const
        ).map((mode) => (
          <button
            key={mode.id}
            type="button"
            onClick={() => setAuthMode(mode.id)}
            className={cn(
              "rounded-pill flex-1 px-3 py-1.5 text-xs font-medium transition-colors",
              authMode === mode.id
                ? "bg-accent text-white"
                : "text-text-secondary hover:text-text-primary",
            )}
          >
            {mode.label}
          </button>
        ))}
      </div>

      <form onSubmit={handleSubmit} className="mt-5 space-y-4">
        {/* Nom affiché */}
        <div>
          <label className="text-text-secondary mb-1.5 block text-xs font-medium">
            Nom affiché <span className="opacity-50">(facultatif)</span>
          </label>
          <input
            type="text"
            value={friendlyName}
            onChange={(e) => setFriendlyName(e.target.value)}
            placeholder={`Mon ${selected}`}
            className="rounded-input border-border bg-surface-alt text-text-primary placeholder:text-text-secondary/50 focus:border-accent w-full border px-3.5 py-2.5 text-sm transition-colors outline-none"
          />
        </div>

        {/* URL */}
        <div>
          <label className="text-text-secondary mb-1.5 block text-xs font-medium">
            Adresse (URL)
          </label>
          <input
            type="text"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://monserveur.exemple.com"
            required
            className="rounded-input border-border bg-surface-alt text-text-primary placeholder:text-text-secondary/50 focus:border-accent w-full border px-3.5 py-2.5 text-sm transition-colors outline-none"
          />
        </div>

        {authMode === "apikey" ? (
          <div>
            <label className="text-text-secondary mb-1.5 block text-xs font-medium">
              Clé API
            </label>
            <input
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="votre-clé-api"
              required
              autoComplete="off"
              className="rounded-input border-border bg-surface-alt text-text-primary placeholder:text-text-secondary/50 focus:border-accent w-full border px-3.5 py-2.5 font-mono text-sm transition-colors outline-none"
            />
          </div>
        ) : (
          <>
            <div>
              <label className="text-text-secondary mb-1.5 block text-xs font-medium">
                Nom d&apos;utilisateur
              </label>
              <input
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="votre identifiant"
                required
                autoComplete="username"
                className="rounded-input border-border bg-surface-alt text-text-primary placeholder:text-text-secondary/50 focus:border-accent w-full border px-3.5 py-2.5 text-sm transition-colors outline-none"
              />
            </div>

            <div>
              <label className="text-text-secondary mb-1.5 block text-xs font-medium">
                Mot de passe
              </label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                required
                autoComplete="current-password"
                className="rounded-input border-border bg-surface-alt text-text-primary placeholder:text-text-secondary/50 focus:border-accent w-full border px-3.5 py-2.5 text-sm transition-colors outline-none"
              />
            </div>
          </>
        )}

        {error && (
          <p className="bg-error/10 border-error/30 text-error rounded-md border px-3 py-2 text-xs">
            {error}
          </p>
        )}

        <div className="flex flex-col gap-2 pt-1">
          <Button
            type="submit"
            variant="primary"
            className="w-full"
            disabled={loading}
          >
            {loading ? "Connexion…" : submitLabel}
          </Button>

          {skipLabel && onSkip && (
            <Button
              type="button"
              variant="ghost"
              className="text-text-secondary w-full"
              onClick={onSkip}
            >
              {skipLabel}
            </Button>
          )}

          {onBack && (
            <Button
              type="button"
              variant="ghost"
              className="text-text-secondary w-full"
              onClick={onBack}
            >
              ← Retour
            </Button>
          )}
        </div>
      </form>
    </div>
  );
}

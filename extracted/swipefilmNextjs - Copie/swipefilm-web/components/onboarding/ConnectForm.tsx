"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { cn } from "@/lib/utils";

interface ServerOption {
  id: string;
  label: string;
}

interface ConnectFormProps {
  title: string;
  subtitle?: string;
  options: ServerOption[];
  onSubmit: (data: {
    type: string;
    friendlyName: string;
    url: string;
    username: string;
    password: string;
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
  const [friendlyName, setFriendlyName] = useState("");
  const [url, setUrl] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const isOverseerr = selected === "overseerr";

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!username.trim() || !password.trim()) {
      setError("Renseigne le nom d'utilisateur et le mot de passe.");
      return;
    }
    if (!isOverseerr && !url.trim()) {
      setError("Renseigne l'adresse du serveur.");
      return;
    }

    setLoading(true);
    try {
      await onSubmit({
        type: selected,
        friendlyName: friendlyName.trim() || `Mon ${selected}`,
        url: url.trim(),
        username: username.trim(),
        password: password.trim(),
      });
    } catch {
      setError("La connexion a échoué — vérifie l'adresse et les identifiants.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="relative z-10 w-full max-w-md rounded-card border border-border bg-surface/70 backdrop-blur-xl p-8 shadow-2xl">
      <div className="mb-4 text-center">
        <span className="font-display text-xl font-bold text-text-primary">
          Swipe<span className="text-accent">Film</span>
        </span>
      </div>

      <h1 className="font-display text-xl font-semibold text-text-primary text-center">
        {title}
      </h1>
      {subtitle && (
        <p className="mt-1.5 text-sm text-text-secondary text-center">{subtitle}</p>
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
                "rounded-pill px-4 py-1.5 text-[13px] font-medium border transition-colors",
                selected === opt.id
                  ? "border-accent bg-accent/20 text-accent"
                  : "border-border text-text-secondary hover:text-text-primary"
              )}
            >
              {opt.label}
            </button>
          ))}
        </div>
      )}

      <form onSubmit={handleSubmit} className="mt-5 space-y-4">

        {/* Nom affiché */}
        <div>
          <label className="block text-xs font-medium text-text-secondary mb-1.5">
            Nom affiché{" "}
            <span className="opacity-50">(facultatif)</span>
          </label>
          <input
            type="text"
            value={friendlyName}
            onChange={(e) => setFriendlyName(e.target.value)}
            placeholder={`Mon ${selected}`}
            className="w-full rounded-input border border-border bg-surface-alt px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-secondary/50 outline-none focus:border-accent transition-colors"
          />
        </div>

        {/* URL — pas pour Overseerr qui gère ça différemment */}
        <div>
          <label className="block text-xs font-medium text-text-secondary mb-1.5">
            Adresse (URL)
          </label>
          <input
            type="text"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://monserveur.exemple.com"
            required={!isOverseerr}
            className="w-full rounded-input border border-border bg-surface-alt px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-secondary/50 outline-none focus:border-accent transition-colors"
          />
        </div>

        {/* Nom d'utilisateur */}
        <div>
          <label className="block text-xs font-medium text-text-secondary mb-1.5">
            {isOverseerr ? "Clé API" : "Nom d'utilisateur"}
          </label>
          <input
            type={isOverseerr ? "password" : "text"}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder={isOverseerr ? "votre-clé-api" : "votre identifiant"}
            required
            autoComplete="username"
            className="w-full rounded-input border border-border bg-surface-alt px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-secondary/50 outline-none focus:border-accent transition-colors"
          />
        </div>

        {/* Mot de passe — masqué pour Overseerr */}
        {!isOverseerr && (
          <div>
            <label className="block text-xs font-medium text-text-secondary mb-1.5">
              Mot de passe
            </label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              required
              autoComplete="current-password"
              className="w-full rounded-input border border-border bg-surface-alt px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-secondary/50 outline-none focus:border-accent transition-colors"
            />
          </div>
        )}

        {error && (
          <p className="rounded-md bg-error/10 border border-error/30 px-3 py-2 text-xs text-error">
            {error}
          </p>
        )}

        <div className="flex flex-col gap-2 pt-1">
          <Button type="submit" variant="primary" className="w-full" disabled={loading}>
            {loading ? "Connexion…" : submitLabel}
          </Button>

          {skipLabel && onSkip && (
            <Button
              type="button"
              variant="ghost"
              className="w-full text-text-secondary"
              onClick={onSkip}
            >
              {skipLabel}
            </Button>
          )}

          {onBack && (
            <Button
              type="button"
              variant="ghost"
              className="w-full text-text-secondary"
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

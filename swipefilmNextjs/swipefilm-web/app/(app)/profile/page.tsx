"use client";

import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import Link from "next/link";
import { hasFlag } from "country-flag-icons";
import "country-flag-icons/3x2/flags.css";
import { ChevronDown, Settings2, Radio, LogOut } from "lucide-react";
import { TopNav } from "@/components/layout/TopNav";
import { Button } from "@/components/ui/Button";
import { useAppStore } from "@/lib/store";
import { useAuth } from "@/features/auth/AuthContext";
import { cn } from "@/lib/utils";
import {
  getMyProfile,
  getMyPreferences,
  updateMyPreferences,
  getMyQuota,
  type UserProfile,
  type UserPreferences,
  type UpdatePreferencesPayload,
  type UserQuota,
  type QuotaBucket,
} from "@/features/profile/api";

const COUNTRIES: { code: string; label: string }[] = [
  { code: "FR", label: "France" },
  { code: "BE", label: "Belgique" },
  { code: "CH", label: "Suisse" },
  { code: "CA", label: "Canada" },
  { code: "US", label: "États-Unis" },
  { code: "GB", label: "Royaume-Uni" },
  { code: "IE", label: "Irlande" },
  { code: "DE", label: "Allemagne" },
  { code: "ES", label: "Espagne" },
  { code: "IT", label: "Italie" },
  { code: "PT", label: "Portugal" },
  { code: "NL", label: "Pays-Bas" },
  { code: "LU", label: "Luxembourg" },
  { code: "AT", label: "Autriche" },
  { code: "SE", label: "Suède" },
  { code: "NO", label: "Norvège" },
  { code: "DK", label: "Danemark" },
  { code: "FI", label: "Finlande" },
  { code: "PL", label: "Pologne" },
  { code: "GR", label: "Grèce" },
  { code: "TR", label: "Turquie" },
  { code: "RU", label: "Russie" },
  { code: "MA", label: "Maroc" },
  { code: "DZ", label: "Algérie" },
  { code: "TN", label: "Tunisie" },
  { code: "SN", label: "Sénégal" },
  { code: "CI", label: "Côte d'Ivoire" },
  { code: "JP", label: "Japon" },
  { code: "KR", label: "Corée du Sud" },
  { code: "CN", label: "Chine" },
  { code: "IN", label: "Inde" },
  { code: "AU", label: "Australie" },
  { code: "NZ", label: "Nouvelle-Zélande" },
  { code: "BR", label: "Brésil" },
  { code: "MX", label: "Mexique" },
  { code: "AR", label: "Argentine" },
];

const LANGUAGES: { code: string; label: string }[] = [
  { code: "fr", label: "Français" },
  { code: "en", label: "Anglais" },
  { code: "es", label: "Espagnol" },
  { code: "de", label: "Allemand" },
  { code: "it", label: "Italien" },
  { code: "pt", label: "Portugais" },
  { code: "nl", label: "Néerlandais" },
  { code: "sv", label: "Suédois" },
  { code: "pl", label: "Polonais" },
  { code: "ru", label: "Russe" },
  { code: "tr", label: "Turc" },
  { code: "ar", label: "Arabe" },
  { code: "ja", label: "Japonais" },
  { code: "ko", label: "Coréen" },
  { code: "zh", label: "Chinois" },
  { code: "hi", label: "Hindi" },
];

// ✅ Même technique qu'Overseerr (RegionSelector) — sprites CSS de
// country-flag-icons plutôt que des emojis (rendu cohérent partout, contrairement
// aux emojis drapeau qui ne s'affichent pas sur certaines polices Windows).
// Un <select> natif ne permet pas de mettre une image de fond sur une <option>,
// d'où ce dropdown custom.
function CountrySelect({
  value,
  onChange,
}: {
  value: string;
  onChange: (code: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node))
        setOpen(false);
    }
    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, []);

  const selected = COUNTRIES.find((c) => c.code === value);

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="border-border bg-bg-primary text-text-primary focus:ring-accent/40 focus:border-accent/60 flex w-full items-center gap-2 rounded-lg border px-3 py-2.5 text-sm transition-all focus:ring-2 focus:outline-none"
      >
        {selected && hasFlag(selected.code) && (
          <span
            className={cn(`flag:${selected.code}`, "flex-shrink-0 rounded-sm")}
          />
        )}
        <span className="flex-1 text-left">
          {selected ? selected.label : "Choisir un pays"}
        </span>
        <ChevronDown className="size-4 text-text-secondary" />
      </button>
      {open && (
        <div className="border-border bg-surface absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border shadow-lg">
          {COUNTRIES.map((c) => (
            <button
              key={c.code}
              type="button"
              onClick={() => {
                onChange(c.code);
                setOpen(false);
              }}
              className={cn(
                "hover:bg-surface-alt flex w-full items-center gap-2 px-3 py-2 text-left text-sm",
                c.code === value && "bg-accent/10 text-accent",
              )}
            >
              {hasFlag(c.code) && (
                <span
                  className={cn(`flag:${c.code}`, "flex-shrink-0 rounded-sm")}
                />
              )}
              {c.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export default function ProfilePage() {
  const server = useAppStore((s) => s.server);
  const clearAppStore = useAppStore((s) => s.logout);
  const { logout: authLogout } = useAuth();

  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [quota, setQuota] = useState<UserQuota | null>(null);
  const [loading, setLoading] = useState(true);

  const [form, setForm] = useState<UpdatePreferencesPayload>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    async function load() {
      setLoading(true);
      try {
        const [p, prefs, q] = await Promise.all([
          getMyProfile(),
          getMyPreferences(),
          getMyQuota(),
        ]);
        setProfile(p);
        setQuota(q);
        setForm({
          locale: prefs.locale,
          region: prefs.region,
          originalLanguage: prefs.originalLanguage,
          autoRequestOnSwipe: prefs.autoRequestOnSwipe,
        });
      } catch {
        /* silencieux */
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  async function handleSavePreferences(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    setSaved(false);
    try {
      await updateMyPreferences(form);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch {
      /* silencieux */
    } finally {
      setSaving(false);
    }
  }

  async function handleLogout() {
    clearAppStore();
    await authLogout();
  }

  const initials = profile?.displayName
    ? profile.displayName
        .trim()
        .split(/\s+/)
        .map((p) => p[0])
        .slice(0, 2)
        .join("")
        .toUpperCase()
    : "?";

  const serverTypeLabel =
    server?.type === 1 || server?.type === "jellyfin" ? "Jellyfin" : "Plex";

  return (
    <div className="min-h-screen">
      <TopNav />
      <div className="mx-auto max-w-2xl px-4 py-8 sm:px-6">
        {loading ? (
          <div className="flex justify-center py-20">
            <div className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
          </div>
        ) : (
          <div className="space-y-6">
            {/* ── En-tête ── */}
            <div className="border-border bg-surface flex items-center gap-4 rounded-xl border p-6">
              <div className="bg-accent/15 text-accent flex h-16 w-16 flex-shrink-0 items-center justify-center rounded-full text-xl font-bold">
                {initials}
              </div>
              <div className="min-w-0 flex-1">
                <h1 className="text-text-primary truncate text-xl font-bold">
                  {profile?.displayName}
                </h1>
                <p className="text-text-secondary truncate text-sm">
                  {profile?.email}
                </p>
                {profile?.isAdmin && (
                  <span className="border-accent/30 bg-accent/10 text-accent rounded-pill mt-1.5 inline-flex items-center border px-2 py-0.5 text-[11px] font-medium">
                    Administrateur
                  </span>
                )}
              </div>
              <Link
                href="/settings"
                title="Paramètres du compte"
                className="bg-surface-alt border-border text-text-secondary hover:text-text-primary flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full border transition-colors"
              >
                <Settings2 className="size-4" />
              </Link>
            </div>

            {/* ── Serveur média ── */}
            <div className="border-border bg-surface rounded-xl border p-6">
              <h2 className="text-text-primary mb-4 text-sm font-semibold tracking-wide uppercase">
                Serveur média
              </h2>
              {server ? (
                <div className="flex items-center gap-3">
                  <div className="bg-accent/10 border-accent/20 flex h-9 w-9 items-center justify-center rounded-lg border">
                    <Radio className="size-4 text-accent" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-text-primary text-sm font-medium">
                      {server.friendlyName ?? serverTypeLabel}
                    </p>
                    <p className="text-text-secondary text-xs">
                      {server.lastSyncAt
                        ? `Dernière synchro : ${new Date(server.lastSyncAt).toLocaleString()}`
                        : "Jamais synchronisé"}
                    </p>
                  </div>
                </div>
              ) : (
                <p className="text-text-secondary text-sm">
                  Aucun serveur configuré.
                </p>
              )}
            </div>

            {/* ── Quota ── */}
            {quota && (
              <div className="border-border bg-surface rounded-xl border p-6">
                <h2 className="text-text-primary mb-4 text-sm font-semibold tracking-wide uppercase">
                  Quota de requêtes
                </h2>
                <div className="space-y-4">
                  <QuotaRow label="Films" bucket={quota.movie} />
                  <QuotaRow label="Séries" bucket={quota.tv} />
                </div>
              </div>
            )}

            {/* ── Préférences ── */}
            <form
              onSubmit={handleSavePreferences}
              className="border-border bg-surface rounded-xl border p-6"
            >
              <h2 className="text-text-primary mb-4 text-sm font-semibold tracking-wide uppercase">
                Préférences
              </h2>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className="text-text-secondary mb-1.5 block text-xs font-medium">
                    Langue
                  </label>
                  <select
                    value={form.locale ?? ""}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, locale: e.target.value }))
                    }
                    className="border-border bg-bg-primary text-text-primary focus:ring-accent/40 focus:border-accent/60 w-full rounded-lg border px-3 py-2.5 text-sm transition-all focus:ring-2 focus:outline-none"
                  >
                    <option value="" disabled>
                      Choisir une langue
                    </option>
                    {LANGUAGES.map((l) => (
                      <option key={l.code} value={l.code}>
                        {l.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="text-text-secondary mb-1.5 block text-xs font-medium">
                    Pays
                  </label>
                  <CountrySelect
                    value={form.region ?? ""}
                    onChange={(region) =>
                      setForm((f) => ({ ...f, region }))
                    }
                  />
                </div>
                <label className="flex items-center gap-2 text-sm sm:col-span-2">
                  <input
                    type="checkbox"
                    checked={form.autoRequestOnSwipe ?? false}
                    onChange={(e) =>
                      setForm((f) => ({
                        ...f,
                        autoRequestOnSwipe: e.target.checked,
                      }))
                    }
                    className="accent-accent h-4 w-4"
                  />
                  <span className="text-text-primary">
                    Requêter automatiquement quand je like un film indisponible
                  </span>
                </label>
              </div>
              <div className="mt-5 flex items-center gap-3">
                <Button
                  type="submit"
                  variant="primary"
                  size="sm"
                  disabled={saving}
                  className="rounded-lg"
                >
                  {saving ? "Enregistrement…" : "Enregistrer"}
                </Button>
                {saved && (
                  <span className="text-success text-xs">
                    Préférences mises à jour
                  </span>
                )}
              </div>
            </form>

            {/* ── Compte ── */}
            <div className="border-border bg-surface flex flex-wrap items-center justify-between gap-3 rounded-xl border p-6">
              <div>
                <p className="text-text-primary text-sm font-medium">
                  Modifier mon profil ou mot de passe
                </p>
                <p className="text-text-secondary text-xs">
                  Nom affiché, email, mot de passe — dans les paramètres
                </p>
              </div>
              <Link href="/settings">
                <Button variant="secondary" size="sm" className="rounded-lg">
                  Aller aux paramètres
                </Button>
              </Link>
            </div>

            <Button
              variant="danger"
              size="sm"
              onClick={handleLogout}
              className="w-full justify-center rounded-lg"
            >
              <LogOut className="size-4" />
              Se déconnecter
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}

function QuotaRow({ label, bucket }: { label: string; bucket: QuotaBucket }) {
  const pct =
    !bucket.isUnlimited && bucket.limit && bucket.used !== null
      ? Math.min(100, (bucket.used / bucket.limit) * 100)
      : 0;

  return (
    <div>
      <div className="mb-1.5 flex items-center justify-between text-sm">
        <span className="text-text-primary font-medium">{label}</span>
        <span className="text-text-secondary text-xs">
          {bucket.isUnlimited
            ? "Illimité"
            : bucket.used !== null
              ? `${bucket.used} / ${bucket.limit} (${bucket.days}j)`
              : `Limite : ${bucket.limit} / ${bucket.days}j`}
        </span>
      </div>
      {!bucket.isUnlimited && (
        <div className="bg-surface-alt h-1.5 w-full overflow-hidden rounded-full">
          <div
            className="bg-accent h-full rounded-full transition-all"
            style={{ width: `${pct}%` }}
          />
        </div>
      )}
    </div>
  );
}

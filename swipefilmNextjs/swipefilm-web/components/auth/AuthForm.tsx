"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { cn } from "@/lib/utils";

export interface AuthField {
  name: string;
  label: string;
  type: "text" | "email" | "password";
  placeholder: string;
}

interface AuthFormProps {
  title: string;
  subtitle: string;
  fields: AuthField[];
  submitLabel: string;
  footerText: string;
  footerLinkLabel: string;
  footerLinkHref: string;
  onSubmit: (values: Record<string, string>) => Promise<void>;
}

export function AuthForm({
  title,
  subtitle,
  fields,
  submitLabel,
  footerText,
  footerLinkLabel,
  footerLinkHref,
  onSubmit,
}: AuthFormProps) {
  const [values, setValues] = useState<Record<string, string>>(
    Object.fromEntries(fields.map((f) => [f.name, ""])),
  );
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  function handleChange(name: string, value: string) {
    setValues((prev) => ({ ...prev, [name]: value }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await onSubmit(values);
    } catch (err: unknown) {
      const msg =
        err instanceof Error ? err.message : "Une erreur est survenue.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="rounded-card border-border bg-surface/70 relative z-10 w-full max-w-md border p-8 shadow-2xl backdrop-blur-xl">
      {/* Logo */}
      <div className="mb-6 text-center">
        <span className="font-display text-text-primary text-2xl font-bold tracking-tight">
          Swipe<span className="text-accent">Film</span>
        </span>
      </div>

      <h1 className="font-display text-text-primary text-center text-xl font-semibold">
        {title}
      </h1>
      <p className="text-text-secondary mt-1 text-center text-sm">{subtitle}</p>

      <form onSubmit={handleSubmit} className="mt-6 space-y-4">
        {fields.map((field) => (
          <div key={field.name}>
            <label className="text-text-secondary mb-1.5 block text-xs font-medium">
              {field.label}
            </label>
            <input
              type={field.type}
              value={values[field.name] ?? ""}
              onChange={(e) => handleChange(field.name, e.target.value)}
              placeholder={field.placeholder}
              autoComplete={
                field.type === "password" ? "current-password" : field.name
              }
              required
              className="rounded-input border-border bg-surface-alt text-text-primary placeholder:text-text-secondary/50 focus:border-accent w-full border px-3.5 py-2.5 text-sm transition-colors outline-none"
            />
          </div>
        ))}

        {error && (
          <p className="bg-error/10 border-error/30 text-error rounded-md border px-3 py-2 text-xs">
            {error}
          </p>
        )}

        <Button
          type="submit"
          variant="primary"
          className="mt-2 w-full"
          disabled={loading}
        >
          {loading ? "Chargement…" : submitLabel}
        </Button>
      </form>

      <p className="text-text-secondary mt-5 text-center text-xs">
        {footerText}{" "}
        <Link
          href={footerLinkHref}
          className="text-accent font-medium hover:underline"
        >
          {footerLinkLabel}
        </Link>
      </p>
    </div>
  );
}

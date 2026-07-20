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
    Object.fromEntries(fields.map((f) => [f.name, ""]))
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
    <div className="relative z-10 w-full max-w-md rounded-card border border-border bg-surface/70 backdrop-blur-xl p-8 shadow-2xl">
      {/* Logo */}
      <div className="mb-6 text-center">
        <span className="font-display text-2xl font-bold tracking-tight text-text-primary">
          Swipe<span className="text-accent">Film</span>
        </span>
      </div>

      <h1 className="font-display text-xl font-semibold text-text-primary text-center">
        {title}
      </h1>
      <p className="mt-1 text-sm text-text-secondary text-center">{subtitle}</p>

      <form onSubmit={handleSubmit} className="mt-6 space-y-4">
        {fields.map((field) => (
          <div key={field.name}>
            <label className="block text-xs font-medium text-text-secondary mb-1.5">
              {field.label}
            </label>
            <input
              type={field.type}
              value={values[field.name]}
              onChange={(e) => handleChange(field.name, e.target.value)}
              placeholder={field.placeholder}
              autoComplete={
                field.type === "password" ? "current-password" : field.name
              }
              required
              className="w-full rounded-input border border-border bg-surface-alt px-3.5 py-2.5 text-sm text-text-primary placeholder:text-text-secondary/50 outline-none focus:border-accent transition-colors"
            />
          </div>
        ))}

        {error && (
          <p className="rounded-md bg-error/10 border border-error/30 px-3 py-2 text-xs text-error">
            {error}
          </p>
        )}

        <Button
          type="submit"
          variant="primary"
          className="w-full mt-2"
          disabled={loading}
        >
          {loading ? "Chargement…" : submitLabel}
        </Button>
      </form>

      <p className="mt-5 text-center text-xs text-text-secondary">
        {footerText}{" "}
        <Link
          href={footerLinkHref}
          className="text-accent hover:underline font-medium"
        >
          {footerLinkLabel}
        </Link>
      </p>
    </div>
  );
}

"use client";

import { useRouter } from "next/navigation";
import { PosterBackdrop } from "@/components/onboarding/PosterBackdrop";
import { AuthForm } from "@/components/auth/AuthForm";
import { register, fetchMe } from "@/lib/auth";
import { useAppStore } from "@/lib/store";

export default function SignupPage() {
  const router = useRouter();
  const setUser = useAppStore((s) => s.setUser);

  async function handleSignup(values: Record<string, string>) {
    if (values.password !== values.confirmPassword) {
      throw new Error("Les mots de passe ne correspondent pas.");
    }

    await register(values.email, values.password, values.displayName);
    const me = await fetchMe();
    setUser({
      id: me.id,
      username: me.username,
      name: (me as { displayName?: string }).displayName ?? me.username,
    });

    router.replace("/onboarding");
  }
  return (
    <div className="relative flex min-h-screen items-center justify-center px-4 py-12">
      <PosterBackdrop />
      <AuthForm
        title="Créer un compte"
        subtitle="Commencez à découvrir des films qui vous ressemblent."
        fields={[
          {
            name: "displayName",
            label: "Nom affiché",
            type: "text",
            placeholder: "Jean Dupont",
          },
          {
            name: "email",
            label: "Adresse e-mail",
            type: "email",
            placeholder: "vous@exemple.com",
          },
          {
            name: "password",
            label: "Mot de passe",
            type: "password",
            placeholder: "Minimum 8 caractères",
          },
          {
            name: "confirmPassword",
            label: "Confirmer le mot de passe",
            type: "password",
            placeholder: "••••••••",
          },
        ]}
        submitLabel="Créer mon compte"
        footerText="Déjà un compte ?"
        footerLinkLabel="Se connecter"
        footerLinkHref="/login"
        onSubmit={handleSignup}
      />
    </div>
  );
}

"use client";

import { useRouter } from "next/navigation";
import { PosterBackdrop } from "@/components/onboarding/PosterBackdrop";
import { AuthForm } from "@/components/auth/AuthForm";
import { login, fetchMe } from "@/lib/auth";
import { useAppStore } from "@/lib/store";

export default function LoginPage() {
  const router = useRouter();
  const setUser = useAppStore((s) => s.setUser);
  const activeServer = useAppStore((s) => s.activeServer);

  async function handleLogin(values: Record<string, string>) {
    await login(values.email, values.password);
    const me = await fetchMe();
    setUser({
      id: me.id,
      email: me.email,
      name: (me as { displayName?: string }).displayName ?? me.email,
    });

    // Redirige vers onboarding si pas encore de serveur, sinon home
    router.replace(activeServer ? "/home" : "/onboarding");
  }

  return (
    <div className="relative min-h-screen flex items-center justify-center px-4 py-12">
      <PosterBackdrop />
      <AuthForm
        title="Bon retour !"
        subtitle="Connectez-vous pour retrouver vos recommandations."
        fields={[
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
            placeholder: "••••••••",
          },
        ]}
        submitLabel="Se connecter"
        footerText="Pas encore de compte ?"
        footerLinkLabel="Créer un compte"
        footerLinkHref="/signup"
        onSubmit={handleLogin}
      />
    </div>
  );
}

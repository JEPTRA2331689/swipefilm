"use client";

import { useRouter } from "next/navigation";
import { PosterBackdrop } from "@/components/onboarding/PosterBackdrop";
import { AuthForm } from "@/features/auth/components/AuthForm";
import { fetchMe } from "@/features/auth/api";
import { normalizeServer } from "@/features/media-server/api";
import { useAuth } from "@/features/auth/AuthContext";
import { useAppStore } from "@/lib/store";

export default function LoginPage() {
  const router = useRouter();
  const { login } = useAuth();
  const { setUser, setServer } = useAppStore();

  async function handleLogin(values: Record<string, string>) {
    await login(values.username, values.password);

    const me = await fetchMe();
    setUser(me);

    const server = me.server ? normalizeServer(me.server) : null;
    setServer(server);

    router.replace(server ? "/home" : "/onboarding");
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center px-4 py-12">
      <PosterBackdrop />

      <AuthForm
        title="Bon retour !"
        subtitle="Connectez-vous pour retrouver vos recommandations."
        fields={[
          {
            name: "username",
            label: "Nom d'utilisateur",
            type: "text",
            placeholder: "votre_nom_utilisateur",
          },
          {
            name: "password",
            label: "Mot de passe",
            type: "password",
            placeholder: "••••••••",
          },
        ]}
        submitLabel="Se connecter"
        onSubmit={handleLogin}
      />
    </div>
  );
}

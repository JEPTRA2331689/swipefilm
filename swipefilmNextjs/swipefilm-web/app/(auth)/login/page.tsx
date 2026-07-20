"use client";

import { useRouter } from "next/navigation";
import { PosterBackdrop } from "@/components/onboarding/PosterBackdrop";
import { AuthForm } from "@/components/auth/AuthForm";
import { fetchMe, normalizeServer } from "@/lib/auth";
import { useAuth } from "@/context/AuthContext";
import { useAppStore } from "@/lib/store";
import type { UserServer } from "@/types";

export default function LoginPage() {
  const router = useRouter();
  const { login } = useAuth();
  const { setUser, setServer } = useAppStore();
  const auth = useAuth();

  async function handleLogin(values: Record<string, string>) {
    console.log("Login form submitted with values:", values);
    await login(values.username, values.password);

    console.log(auth);
    const me = await fetchMe();

    setUser({
      id: me.id,
      username: me.username,
      name: (me as { displayName?: string }).displayName ?? me.username,
    });

    const raw = (me as { server?: UserServer | null }).server ?? null;
    const server = raw ? normalizeServer(raw) : null;
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
        footerText="Pas encore de compte ?"
        footerLinkLabel="Créer un compte"
        footerLinkHref="/signup"
        onSubmit={handleLogin}
      />
    </div>
  );
}

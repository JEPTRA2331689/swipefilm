import type { Metadata, Viewport } from "next";
import "./globals.css";

import { AuthProvider } from "@/features/auth/AuthContext";

export const metadata: Metadata = {
  metadataBase: new URL("https://swipefilm.app"),

  title: {
    default: "SwipeFilm",
    template: "%s • SwipeFilm",
  },

  description:
    "Découvrez votre prochain film ou votre prochaine série grâce à des recommandations intelligentes.",

  applicationName: "SwipeFilm",

  keywords: [
    "films",
    "séries",
    "streaming",
    "plex",
    "jellyfin",
    "cinéma",
    "recommandation",
    "movie",
    "tv",
  ],

  authors: [{ name: "SwipeFilm" }],

  icons: {
    icon: "/favicon.ico",
    apple: "/apple-touch-icon.png",
  },

  openGraph: {
    title: "SwipeFilm",
    description:
      "Trouvez votre prochain film ou série grâce à des recommandations personnalisées.",
    siteName: "SwipeFilm",
    type: "website",
    locale: "fr_CA",
  },

  twitter: {
    card: "summary_large_image",
    title: "SwipeFilm",
    description: "Découvrez votre prochain film en quelques swipes.",
  },
};

export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: dark)", color: "#09090B" },
    { media: "(prefers-color-scheme: light)", color: "#FFFFFF" },
  ],
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="fr" suppressHydrationWarning>
      <head>
        <script
          dangerouslySetInnerHTML={{
            __html: `
(function () {
  try {
    const stored = localStorage.getItem("swipefilm_theme");

    const theme =
      stored ??
      (window.matchMedia("(prefers-color-scheme: light)").matches
        ? "light"
        : "dark");

    document.documentElement.classList.remove("light", "dark");
    document.documentElement.classList.add(theme);

    document.documentElement.style.colorScheme = theme;

  } catch {
    document.documentElement.classList.add("dark");
    document.documentElement.style.colorScheme = "dark";
  }
})();
`,
          }}
        />

        <link rel="preconnect" href="https://fonts.googleapis.com" />

        <link
          rel="preconnect"
          href="https://fonts.gstatic.com"
          crossOrigin="anonymous"
        />

        <link
          href="https://fonts.googleapis.com/css2?family=Bebas+Neue&family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap"
          rel="stylesheet"
        />
      </head>

      <body className="bg-bg-primary text-text-primary antialiased">
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}

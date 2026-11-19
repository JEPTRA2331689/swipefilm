import type { NextConfig } from "next";

const isDev = process.env.NODE_ENV === "development";

const nextConfig: NextConfig = {
  ...(isDev && {
    async rewrites() {
      return [{ source: "/api/:path*", destination: "http://localhost:5250/api/:path*" }];
    },
  }),
  /* config options here */
  // ✅ Pas de serveur Node en prod — le backend .NET sert ces fichiers
  // statiques directement (même pattern que Radarr/Sonarr). L'optimiseur
  // d'images de Next a besoin d'un serveur qui tourne ; nos images viennent
  // déjà du CDN TMDB, donc on n'y perd rien.
  images: {
    unoptimized: true,
  },
  ...(!isDev && { output: "export" as const }),
};

export default nextConfig;

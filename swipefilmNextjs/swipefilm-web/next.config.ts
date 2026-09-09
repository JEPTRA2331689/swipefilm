import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  // ✅ Pas de serveur Node en prod — le backend .NET sert ces fichiers
  // statiques directement (même pattern que Radarr/Sonarr). L'optimiseur
  // d'images de Next a besoin d'un serveur qui tourne ; nos images viennent
  // déjà du CDN TMDB, donc on n'y perd rien.
  images: {
    unoptimized: true,
  },
  output: "export",
};

export default nextConfig;

"use client";

import { Button } from "@/components/ui/Button";

interface WelcomeStepProps {
  onNext: () => void;
}

export function WelcomeStep({ onNext }: WelcomeStepProps) {
  return (
    /* Conteneur principal responsive (Largeur max contrôlée) */
    <div className="relative w-full max-w-lg aspect-square flex flex-col justify-between p-8 text-center overflow-hidden
      bg-[#262f70]/20 backdrop-blur-2xl rounded-3xl border border-white/10
      before:absolute before:inset-0 before:content-[''] before:opacity-[0.05] before:z-10 before:pointer-events-none
      before:bg-[url('https://www.ui-layouts.com/noise.gif')]"
    >
      {/* Contenu textuel */}
      <div className="flex flex-col gap-4 items-center z-20 my-auto">
        <h1 className="font-display text-4xl font-semibold text-text-primary leading-tight">
          Des films qui vous ressemblent
        </h1>
        <p className="text-[15px] leading-relaxed text-text-secondary line-clamp-6">
          Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod
          tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam,
          quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.
        </p>
      </div>

      {/* Bouton d'action */}
      <Button variant="primary" size="lg" onClick={onNext} className="w-full z-20 mt-auto">
        Configurer
      </Button>
    </div>
  );
}

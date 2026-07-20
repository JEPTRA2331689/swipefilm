"use client";

import { Button } from "@/components/ui/Button";

interface WelcomeStepProps {
  onNext: () => void;
}

export function WelcomeStep({ onNext }: WelcomeStepProps) {
  return (
    /* Conteneur principal responsive (Largeur max contrôlée) */
    <div className="relative flex aspect-square w-full max-w-lg flex-col justify-between overflow-hidden rounded-3xl border border-white/10 bg-[#262f70]/20 p-8 text-center backdrop-blur-2xl before:pointer-events-none before:absolute before:inset-0 before:z-10 before:bg-[url('https://www.ui-layouts.com/noise.gif')] before:opacity-[0.05] before:content-['']">
      {/* Contenu textuel */}
      <div className="z-20 my-auto flex flex-col items-center gap-4">
        <h1 className="font-display text-text-primary text-4xl leading-tight font-semibold">
          Des films qui vous ressemblent
        </h1>
        <p className="text-text-secondary line-clamp-6 text-[15px] leading-relaxed">
          Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do
          eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad
          minim veniam, quis nostrud exercitation ullamco laboris nisi ut
          aliquip ex ea commodo consequat.
        </p>
      </div>

      {/* Bouton d'action */}
      <Button
        variant="primary"
        size="lg"
        onClick={onNext}
        className="z-20 mt-auto w-full"
      >
        Configurer
      </Button>
    </div>
  );
}

import { useState, useCallback, useRef } from "react";

export function useContainerSize() {
  const [size, setSize] = useState({ width: 0, height: 0 });
  
  // Stocker l'observateur pour pouvoir le nettoyer si l'élément change
  const observerRef = useRef<ResizeObserver | null>(null);

  // Une Callback Ref s'exécute à chaque fois que l'élément DOM change ou est monté
  const ref = useCallback((node: HTMLDivElement | null) => {
    // 1. Nettoyer l'ancien observateur si nécessaire
    if (observerRef.current) {
      observerRef.current.disconnect();
      observerRef.current = null;
    }

    // 2. Si l'élément existe, on attache le nouvel observateur
    if (node) {
      const obs = new ResizeObserver(([entry]) => {
        if (!entry) return;
        setSize({
          width: entry.contentRect.width,
          height: entry.contentRect.height,
        });
      });

      obs.observe(node);
      observerRef.current = obs;
    }
  }, []);

  return { ref, size };
}

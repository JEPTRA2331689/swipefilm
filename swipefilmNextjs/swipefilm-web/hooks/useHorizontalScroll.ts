"use client";

import { useCallback, useEffect, useRef, useState } from "react";

// ✅ Un seul hook partagé par SectionRow/GenreCarousel/SimilarMedia — chacun
// gérait déjà peek/snap/scrollbar masquée à sa façon, mais aucun n'avait de
// flèches pour la souris desktop (contrairement au tactile, qui swipe déjà
// nativement). canScrollLeft/Right permet de les cacher en bout de liste
// plutôt que de les laisser cliquables sans effet.
export function useHorizontalScroll<T extends HTMLElement>() {
  const containerRef = useRef<T>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);

  const updateScrollState = useCallback(() => {
    const el = containerRef.current;
    if (!el) return;
    setCanScrollLeft(el.scrollLeft > 4);
    setCanScrollRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 4);
  }, []);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    updateScrollState();

    el.addEventListener("scroll", updateScrollState, { passive: true });
    const resizeObserver = new ResizeObserver(updateScrollState);
    resizeObserver.observe(el);

    return () => {
      el.removeEventListener("scroll", updateScrollState);
      resizeObserver.disconnect();
    };
  }, [updateScrollState]);

  const scrollByPage = useCallback((direction: "left" | "right") => {
    const el = containerRef.current;
    if (!el) return;
    const amount = el.clientWidth * 0.8 * (direction === "left" ? -1 : 1);
    el.scrollBy({ left: amount, behavior: "smooth" });
  }, []);

  return { containerRef, canScrollLeft, canScrollRight, scrollByPage };
}

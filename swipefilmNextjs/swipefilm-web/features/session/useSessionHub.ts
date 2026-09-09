"use client";

import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { SessionMatchResult, SessionState } from "./api";

interface SwipeProgressEvent {
  memberId: string;
  count: number;
}

interface UseSessionHubResult {
  connected: boolean;
  state: SessionState | null;
  matches: SessionMatchResult[] | null;
  swipeProgress: Record<string, number>;
}

// ✅ Même origine que le backend (voir Program.cs — MapHub("/hubs/session")
// servi par le même serveur que l'API) — un chemin relatif suffit, le cookie
// de session HttpOnly part avec la requête de negotiate comme n'importe quel
// autre appel same-origin.
export function useSessionHub(
  code: string | null,
  onStateChange?: (state: SessionState) => void,
): UseSessionHubResult {
  const [connected, setConnected] = useState(false);
  const [state, setState] = useState<SessionState | null>(null);
  const [matches, setMatches] = useState<SessionMatchResult[] | null>(null);
  const [swipeProgress, setSwipeProgress] = useState<Record<string, number>>(
    {},
  );
  const onStateChangeRef = useRef(onStateChange);
  onStateChangeRef.current = onStateChange;

  useEffect(() => {
    if (!code) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/session", { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on("MemberJoined", (next: SessionState) => {
      setState(next);
      onStateChangeRef.current?.(next);
    });
    connection.on("PhaseChanged", (next: SessionState) => {
      setState(next);
      onStateChangeRef.current?.(next);
    });
    connection.on("SwipeProgress", (evt: SwipeProgressEvent) => {
      setSwipeProgress((prev) => ({ ...prev, [evt.memberId]: evt.count }));
    });
    connection.on("MatchesRevealed", (results: SessionMatchResult[]) => {
      setMatches(results);
    });

    connection
      .start()
      .then(() => {
        setConnected(true);
        return connection.invoke("JoinGroup", code);
      })
      .catch(() => setConnected(false));

    return () => {
      connection.stop();
      setConnected(false);
    };
  }, [code]);

  return { connected, state, matches, swipeProgress };
}

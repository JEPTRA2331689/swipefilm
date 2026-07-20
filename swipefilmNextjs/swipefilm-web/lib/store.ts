import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { User, UserServer } from "@/types";

interface AppState {
  user: User | null;
  server: UserServer | null;
  setUser: (user: User | null) => void;
  setServer: (server: UserServer | null) => void;
  logout: () => void;
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      user: null,
      server: null,
      setUser: (user) => set({ user }),
      setServer: (server) => set({ server }),
      logout: () => set({ user: null, server: null }),
    }),
    { name: "swipefilm-app-store" },
  ),
);

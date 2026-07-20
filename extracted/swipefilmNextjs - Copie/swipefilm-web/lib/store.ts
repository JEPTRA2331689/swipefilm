import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { User, UserServer } from "@/types";

interface AppState {
  user: User | null;
  activeServer: UserServer | null;
  servers: UserServer[];
  setUser: (user: User | null) => void;
  setActiveServer: (server: UserServer | null) => void;
  setServers: (servers: UserServer[]) => void;
  logout: () => void;
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      user: null,
      activeServer: null,
      servers: [],
      setUser: (user) => set({ user }),
      setActiveServer: (activeServer) => set({ activeServer }),
      setServers: (servers) => set({ servers }),
      logout: () => set({ user: null, activeServer: null, servers: [] }),
    }),
    { name: "swipefilm-app-store" }
  )
);

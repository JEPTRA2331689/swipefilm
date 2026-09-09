import { api } from "@/lib/api";

// ✅ Correspond à GET /api/users/me (UserSettingsController.MapToDto) — plus
// riche que ce qui est réellement utilisé, mais on garde le typage honnête
// plutôt que de tronquer la réponse.
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  createdAt?: string;
  isAdmin?: boolean;
}

// ✅ Correspond à GET /api/users/me/preferences (UserSettingsController.GetPreferences)
export interface UserPreferences {
  locale: string;
  region: string;
  originalLanguage: string;
  autoRequestOnSwipe: boolean;
  discordId: string | null;
  telegramChatId: string | null;
}

export interface UpdatePreferencesPayload {
  locale?: string;
  region?: string;
  originalLanguage?: string;
  autoRequestOnSwipe?: boolean;
  discordId?: string;
  telegramChatId?: string;
}

export interface QuotaBucket {
  limit: number | null; // null = illimité
  days: number | null;
  used: number | null;
  remaining: number | null;
  isUnlimited: boolean;
}

// ✅ Correspond à GET /api/users/me/quota (UserSettingsController.GetMyQuota)
export interface UserQuota {
  movie: QuotaBucket;
  tv: QuotaBucket;
}

export async function getMyProfile(): Promise<UserProfile> {
  return api.get<UserProfile>("/api/users/me");
}

export async function updateMyProfile(
  data: Partial<Pick<UserProfile, "email" | "displayName">>,
): Promise<UserProfile> {
  return api.put<UserProfile>("/api/users/me", data);
}

export async function getMyPreferences(): Promise<UserPreferences> {
  return api.get<UserPreferences>("/api/users/me/preferences");
}

export async function updateMyPreferences(
  prefs: UpdatePreferencesPayload,
): Promise<UserPreferences> {
  return api.put<UserPreferences>("/api/users/me/preferences", prefs);
}

export async function getMyQuota(): Promise<UserQuota> {
  return api.get<UserQuota>("/api/users/me/quota");
}

export async function changePassword(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  await api.put("/api/users/me/password", { currentPassword, newPassword });
}

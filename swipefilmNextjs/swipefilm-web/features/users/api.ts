import { api } from "@/lib/api";
import type { UserProfile, UserPreferences } from "@/features/profile/api";

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  permissions: number;
  createdAt?: string;
}

export async function getUsers(): Promise<UserListItem[]> {
  return api.get<UserListItem[]>("/api/users");
}

export async function getUser(userId: string): Promise<UserListItem> {
  return api.get<UserListItem>(`/api/users/${userId}`);
}

export async function updateUser(
  userId: string,
  data: Partial<Pick<UserProfile, "email" | "displayName">>,
): Promise<UserListItem> {
  return api.put<UserListItem>(`/api/users/${userId}`, data);
}

export async function getUserPreferences(
  userId: string,
): Promise<UserPreferences> {
  return api.get<UserPreferences>(`/api/users/${userId}/preferences`);
}

export async function updateUserPreferencesById(
  userId: string,
  prefs: UserPreferences,
): Promise<UserPreferences> {
  return api.put<UserPreferences>(`/api/users/${userId}/preferences`, prefs);
}

// ✅ Correspond à GET /api/users/{userId}/quota (UserSettingsController.GetUserQuota)
// — forme différente de /me/quota : pas de Used/Remaining, juste la config.
export interface AdminQuotaBucket {
  limit: number | null;
  days: number | null;
  isUnlimited: boolean;
  isOverride: boolean;
}

export interface AdminUserQuota {
  movie: AdminQuotaBucket;
  tv: AdminQuotaBucket;
}

export interface UpdateUserQuotaPayload {
  movieQuotaLimit?: number | null;
  movieQuotaDays?: number | null;
  tvQuotaLimit?: number | null;
  tvQuotaDays?: number | null;
}

export async function getUserQuota(userId: string): Promise<AdminUserQuota> {
  return api.get<AdminUserQuota>(`/api/users/${userId}/quota`);
}

export async function updateUserQuota(
  userId: string,
  quota: UpdateUserQuotaPayload,
): Promise<AdminUserQuota> {
  return api.put<AdminUserQuota>(`/api/users/${userId}/quota`, quota);
}

export async function deleteUser(userId: string): Promise<void> {
  await api.delete(`/api/users/${userId}`);
}

export async function updateUserPermissions(
  userId: string,
  permissions: number,
): Promise<void> {
  await api.put(`/api/permissions/${userId}`, { permissions });
}

export async function applyPermissionPreset(
  userId: string,
  preset: "none" | "user" | "moderator" | "admin",
): Promise<void> {
  await api.put(`/api/permissions/${userId}/preset`, { preset });
}

export async function getDefaultPermissions(): Promise<{
  defaultPermissions: number;
}> {
  return api.get("/api/permissions/defaults");
}

export async function updateDefaultPermissions(
  permissions: number,
): Promise<void> {
  await api.put("/api/permissions/defaults", { permissions });
}

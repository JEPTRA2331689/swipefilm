import { useAuth } from "@/features/auth/AuthContext";
import { Permission } from "@/lib/permissions";

export function usePermission(permission: Permission): boolean {
  const { hasPermission } = useAuth();
  return hasPermission(permission);
}

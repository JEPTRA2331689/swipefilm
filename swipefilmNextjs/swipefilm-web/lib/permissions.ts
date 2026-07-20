export enum Permission {
  None = 0,
  Admin = 2,
  ManageUsers = 8,
  CanRequest = 32,
  AutoApprove = 128,
  ViewRequests = 1024,
  ManageRequests = 2048,
  CanSwipe = 4096,
  ViewOthersHistory = 8192,
  ViewAdminDashboard = 16384,
  // Combinaisons prédéfinies
  DefaultUser = 4128, // CanSwipe | CanRequest
  Moderator = 7424, // CanSwipe | CanRequest | AutoApprove | ViewRequests | ManageRequests
  FullAdmin = 34022,
}

export function hasPermission(
  userPerms: number,
  required: Permission,
): boolean {
  if (required === Permission.None) return true;
  if ((userPerms & Permission.Admin) !== 0) return true;
  return (userPerms & required) !== 0;
}

export function hasAnyPermission(
  userPerms: number,
  ...permissions: Permission[]
): boolean {
  return permissions.some((p) => hasPermission(userPerms, p));
}

export function hasAllPermissions(
  userPerms: number,
  ...permissions: Permission[]
): boolean {
  return permissions.every((p) => hasPermission(userPerms, p));
}

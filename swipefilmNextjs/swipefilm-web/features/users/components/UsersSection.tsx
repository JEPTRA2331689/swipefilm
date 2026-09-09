"use client";

import { useState, useEffect, useCallback } from "react";
import { Trash2 } from "lucide-react";
import {
  getUsers,
  getUserQuota,
  applyPermissionPreset,
  updateUserPermissions,
  updateUser,
  updateUserQuota,
  deleteUser,
  type UserListItem,
  type AdminUserQuota,
} from "@/features/users/api";
import { PERMISSION_BITS, PRESETS } from "@/features/users/constants";
import { PermToggle } from "@/features/users/components/PermToggle";
import { Permission } from "@/lib/permissions";
import {
  SectionCard,
  CardBody,
  FieldLabel,
  FieldInput,
  BtnPrimary,
  BtnSecondary,
  BtnDanger,
  Spinner,
} from "@/shared/ui/FormPrimitives";
import { cn } from "@/lib/utils";

type UserTab = "permissions" | "profil" | "quota";

export function UsersSection() {
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<UserTab>("permissions");
  const [editPerms, setEditPerms] = useState(0);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [quota, setQuota] = useState<AdminUserQuota | null>(null);
  const [editMovieQuotaLimit, setEditMovieQuotaLimit] = useState("");
  const [editTvQuotaLimit, setEditTvQuotaLimit] = useState("");
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setUsers(await getUsers());
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function startEdit(user: UserListItem) {
    setEditing(user.id);
    setActiveTab("permissions");
    setEditPerms(user.permissions);
    setEditDisplayName(user.displayName);
    setEditEmail(user.email);
    setError(null);
    setQuota(null);
    try {
      const q = await getUserQuota(user.id);
      setQuota(q);
      setEditMovieQuotaLimit(q.movie.limit === null ? "" : String(q.movie.limit));
      setEditTvQuotaLimit(q.tv.limit === null ? "" : String(q.tv.limit));
    } catch {
      /* quota optionnel */
    }
  }

  async function handleApplyPreset(
    userId: string,
    preset: "none" | "user" | "moderator" | "admin",
  ) {
    setSaving(true);
    try {
      await applyPermissionPreset(userId, preset);
      await load();
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleSavePerms(userId: string) {
    setSaving(true);
    try {
      await updateUserPermissions(userId, editPerms);
      await load();
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveProfil(userId: string) {
    setSaving(true);
    try {
      await updateUser(userId, {
        displayName: editDisplayName,
        email: editEmail,
      });
      await load();
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveQuota(userId: string) {
    setSaving(true);
    try {
      const updated = await updateUserQuota(userId, {
        movieQuotaLimit: editMovieQuotaLimit === "" ? null : parseInt(editMovieQuotaLimit, 10),
        tvQuotaLimit: editTvQuotaLimit === "" ? null : parseInt(editTvQuotaLimit, 10),
      });
      setQuota(updated);
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(userId: string) {
    if (!confirm("Supprimer cet utilisateur ?")) return;
    setDeleting(userId);
    try {
      await deleteUser(userId);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setDeleting(null);
    }
  }

  if (loading)
    return (
      <SectionCard>
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      </SectionCard>
    );

  return (
    <div className="space-y-3">
      {error && <p className="text-sm text-red-400">{error}</p>}

      {users.map((user) => {
        const isEditingThis = editing === user.id;
        const userIsAdmin = (user.permissions & Permission.Admin) !== 0;

        return (
          <SectionCard key={user.id}>
            {/* User row */}
            <div className="flex items-center gap-4 px-6 py-4">
              <div className="bg-surface-alt border-border flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full border">
                <span className="text-text-secondary text-sm font-bold">
                  {user.displayName[0]?.toUpperCase()}
                </span>
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <p className="text-text-primary truncate text-sm font-semibold">
                    {user.displayName}
                  </p>
                  {userIsAdmin && (
                    <span className="bg-accent/10 text-accent flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-medium tracking-wider uppercase">
                      Admin
                    </span>
                  )}
                </div>
                <p className="text-text-secondary truncate text-xs">
                  {user.email}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <BtnSecondary
                  onClick={() =>
                    isEditingThis ? setEditing(null) : startEdit(user)
                  }
                >
                  {isEditingThis ? "Fermer" : "Modifier"}
                </BtnSecondary>
                <BtnDanger
                  onClick={() => handleDelete(user.id)}
                  disabled={deleting === user.id}
                >
                  {deleting === user.id ? (
                    <Spinner />
                  ) : (
                    <Trash2 className="size-4" />
                  )}
                </BtnDanger>
              </div>
            </div>

            {/* Edit panel */}
            {isEditingThis && (
              <div className="border-border border-t">
                {/* Tabs */}
                <div className="border-border flex border-b px-6">
                  {(["permissions", "profil", "quota"] as UserTab[]).map(
                    (tab) => (
                      <button
                        key={tab}
                        onClick={() => setActiveTab(tab)}
                        className={cn(
                          "-mb-px cursor-pointer border-b-2 px-4 py-3 text-sm font-medium capitalize transition-colors",
                          activeTab === tab
                            ? "border-accent text-accent"
                            : "text-text-secondary hover:text-text-primary border-transparent",
                        )}
                      >
                        {tab}
                      </button>
                    ),
                  )}
                </div>

                <CardBody className="space-y-4">
                  {/* Permissions */}
                  {activeTab === "permissions" && (
                    <>
                      <div className="flex flex-wrap gap-2">
                        {PRESETS.map((p) => (
                          <button
                            key={p.value}
                            onClick={() => handleApplyPreset(user.id, p.value)}
                            disabled={saving}
                            className={cn(
                              "cursor-pointer rounded-lg border px-3 py-1.5 text-xs transition-colors disabled:opacity-40",
                              p.color,
                              "bg-transparent hover:opacity-80",
                            )}
                          >
                            {p.label}
                          </button>
                        ))}
                      </div>
                      <div className="border-border overflow-hidden rounded-lg border">
                        {PERMISSION_BITS.map(
                          ({ value, label, description }) => (
                            <PermToggle
                              key={value}
                              label={label}
                              description={description}
                              checked={(editPerms & value) !== 0}
                              onChange={() =>
                                setEditPerms((p) =>
                                  (p & value) !== 0 ? p & ~value : p | value,
                                )
                              }
                            />
                          ),
                        )}
                      </div>
                      <div className="flex justify-end">
                        <BtnPrimary
                          onClick={() => handleSavePerms(user.id)}
                          disabled={saving}
                        >
                          {saving && <Spinner />} Enregistrer
                        </BtnPrimary>
                      </div>
                    </>
                  )}

                  {/* Profil */}
                  {activeTab === "profil" && (
                    <>
                      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                        <div>
                          <FieldLabel>Nom affiché</FieldLabel>
                          <FieldInput
                            value={editDisplayName}
                            onChange={(e) => setEditDisplayName(e.target.value)}
                          />
                        </div>
                        <div>
                          <FieldLabel>Email</FieldLabel>
                          <FieldInput
                            type="email"
                            value={editEmail}
                            onChange={(e) => setEditEmail(e.target.value)}
                          />
                        </div>
                      </div>
                      <div className="flex justify-end">
                        <BtnPrimary
                          onClick={() => handleSaveProfil(user.id)}
                          disabled={saving}
                        >
                          {saving && <Spinner />} Enregistrer
                        </BtnPrimary>
                      </div>
                    </>
                  )}

                  {/* Quota */}
                  {activeTab === "quota" && (
                    <>
                      {quota && (
                        <div className="bg-bg-primary border-border grid grid-cols-2 gap-4 rounded-lg border p-4">
                          <div>
                            <p className="text-text-secondary text-xs">
                              Films
                            </p>
                            <p className="text-text-primary text-lg font-bold">
                              {quota.movie.isUnlimited
                                ? "Illimité"
                                : `${quota.movie.limit} / ${quota.movie.days}j`}
                            </p>
                          </div>
                          <div>
                            <p className="text-text-secondary text-xs">
                              Séries
                            </p>
                            <p className="text-text-primary text-lg font-bold">
                              {quota.tv.isUnlimited
                                ? "Illimité"
                                : `${quota.tv.limit} / ${quota.tv.days}j`}
                            </p>
                          </div>
                        </div>
                      )}
                      <div className="grid max-w-md grid-cols-2 gap-4">
                        <div>
                          <FieldLabel>Limite films (vide = illimité)</FieldLabel>
                          <FieldInput
                            type="number"
                            min="0"
                            value={editMovieQuotaLimit}
                            onChange={(e) =>
                              setEditMovieQuotaLimit(e.target.value)
                            }
                          />
                        </div>
                        <div>
                          <FieldLabel>Limite séries (vide = illimité)</FieldLabel>
                          <FieldInput
                            type="number"
                            min="0"
                            value={editTvQuotaLimit}
                            onChange={(e) =>
                              setEditTvQuotaLimit(e.target.value)
                            }
                          />
                        </div>
                      </div>
                      <div className="flex justify-end">
                        <BtnPrimary
                          onClick={() => handleSaveQuota(user.id)}
                          disabled={saving}
                        >
                          {saving && <Spinner />} Enregistrer
                        </BtnPrimary>
                      </div>
                    </>
                  )}
                </CardBody>
              </div>
            )}
          </SectionCard>
        );
      })}
    </div>
  );
}

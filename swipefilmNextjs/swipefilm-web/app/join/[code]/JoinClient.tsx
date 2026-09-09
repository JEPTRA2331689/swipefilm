"use client";

import { useStaticRouteId } from "@/hooks/useStaticRouteId";
import { JoinScreen } from "@/features/session/components/JoinScreen";

export default function JoinClient() {
  const code = useStaticRouteId();
  if (!code) return null;
  return <JoinScreen code={code} />;
}

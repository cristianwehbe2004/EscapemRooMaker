import { useCallback, useMemo, useRef, useState } from "react";

type CooldownState = Record<string, number>;

export type CooldownGateResult =
  | { allowed: true; remainingMs: 0 }
  | { allowed: false; remainingMs: number };

export const useActionCooldown = (cooldownMs = 800) => {
  const [cooldowns, setCooldowns] = useState<CooldownState>({});
  const cooldownsRef = useRef<CooldownState>({});

  const runWithCooldown = useCallback(
    async (key: string, action: () => Promise<void>): Promise<CooldownGateResult> => {
      const now = Date.now();
      const expiresAt = cooldownsRef.current[key] ?? 0;
      if (expiresAt > now) {
        return { allowed: false, remainingMs: expiresAt - now };
      }

      const next = {
        ...cooldownsRef.current,
        [key]: now + cooldownMs,
      };
      cooldownsRef.current = next;
      setCooldowns(next);

      try {
        await action();
      } catch (error) {
        const rollback = { ...cooldownsRef.current };
        delete rollback[key];
        cooldownsRef.current = rollback;
        setCooldowns(rollback);
        throw error;
      }

      return { allowed: true, remainingMs: 0 };
    },
    [cooldownMs]
  );

  const isCoolingDown = useCallback(
    (key: string) => {
      const expiresAt = cooldowns[key] ?? 0;
      return expiresAt > Date.now();
    },
    [cooldowns]
  );

  const getRemainingMs = useCallback(
    (key: string) => {
      const expiresAt = cooldowns[key] ?? 0;
      return Math.max(0, expiresAt - Date.now());
    },
    [cooldowns]
  );

  return useMemo(
    () => ({
      runWithCooldown,
      isCoolingDown,
      getRemainingMs,
    }),
    [getRemainingMs, isCoolingDown, runWithCooldown]
  );
};

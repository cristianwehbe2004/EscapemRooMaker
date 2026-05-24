import React, { useState } from "react";
import { registerAccount, signIn, signOut } from "../../auth/authApi";
import { useAuthSession } from "../../auth/authSession";

type AuthMode = "signin" | "register";

type AuthPanelProps = {
  title?: string;
  subtitle?: string;
  guestHint?: string;
};

const roleOptions = ["Player", "Creator", "GM", "Admin"];

const AuthPanel: React.FC<AuthPanelProps> = ({
  title = "Account Access",
  subtitle = "Sign in to use protected features and authenticated play.",
  guestHint,
}) => {
  const { accessToken, refreshToken, user, isAuthenticated, expiresAtUtc } = useAuthSession();
  const [mode, setMode] = useState<AuthMode>("signin");
  const [loginEmail, setLoginEmail] = useState("");
  const [loginPassword, setLoginPassword] = useState("");
  const [registerUsername, setRegisterUsername] = useState("");
  const [registerEmail, setRegisterEmail] = useState("");
  const [registerPassword, setRegisterPassword] = useState("");
  const [registerRole, setRegisterRole] = useState("Player");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSignIn = async () => {
    setBusy(true);
    setError(null);
    try {
      await signIn({
        email: loginEmail.trim(),
        password: loginPassword,
      });
      setLoginPassword("");
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Could not sign in.");
    } finally {
      setBusy(false);
    }
  };

  const handleRegister = async () => {
    setBusy(true);
    setError(null);
    try {
      await registerAccount({
        username: registerUsername.trim(),
        email: registerEmail.trim(),
        password: registerPassword,
        role: registerRole,
      });
      setRegisterPassword("");
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Could not create account.");
    } finally {
      setBusy(false);
    }
  };

  const handleSignOut = async () => {
    setBusy(true);
    setError(null);
    try {
      await signOut(accessToken, refreshToken);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Could not sign out cleanly.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="rounded-3xl border border-slate-700 bg-slate-900/90 p-5 shadow-[0_18px_60px_rgba(15,23,42,0.35)]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-xs uppercase tracking-[0.22em] text-sky-300">Auth</p>
          <h2 className="mt-2 text-2xl font-semibold text-slate-50">{title}</h2>
          <p className="mt-1 max-w-2xl text-sm text-slate-300">{subtitle}</p>
        </div>
        {guestHint ? <p className="max-w-sm text-sm text-slate-400">{guestHint}</p> : null}
      </div>

      {isAuthenticated && user ? (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-emerald-700/50 bg-emerald-950/40 p-4">
          <div>
            <p className="text-sm font-semibold text-emerald-200">Signed in as {user.username}</p>
            <p className="text-sm text-emerald-100/90">
              {user.email} • role: {user.role}
            </p>
            {expiresAtUtc ? (
              <p className="mt-1 text-xs text-emerald-200/80">Access token expires at {new Date(expiresAtUtc).toLocaleString()}.</p>
            ) : null}
          </div>
          <button
            onClick={() => void handleSignOut()}
            disabled={busy}
            className="rounded-xl border border-emerald-400/60 px-4 py-2 text-sm font-medium text-emerald-100 transition hover:bg-emerald-900/50 disabled:cursor-not-allowed disabled:opacity-60"
          >
            Sign Out
          </button>
        </div>
      ) : (
        <div className="mt-4">
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setMode("signin")}
              className={`rounded-xl px-4 py-2 text-sm font-medium ${mode === "signin" ? "bg-sky-500 text-slate-950" : "bg-slate-800 text-slate-200"}`}
            >
              Sign In
            </button>
            <button
              type="button"
              onClick={() => setMode("register")}
              className={`rounded-xl px-4 py-2 text-sm font-medium ${mode === "register" ? "bg-sky-500 text-slate-950" : "bg-slate-800 text-slate-200"}`}
            >
              Register
            </button>
          </div>

          {mode === "signin" ? (
            <div className="mt-4 grid gap-3 md:grid-cols-[1.2fr_1fr_auto]">
              <input
                value={loginEmail}
                onChange={(event) => setLoginEmail(event.target.value)}
                placeholder="Email"
                className="rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-slate-100"
              />
              <input
                type="password"
                value={loginPassword}
                onChange={(event) => setLoginPassword(event.target.value)}
                placeholder="Password"
                className="rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-slate-100"
              />
              <button
                onClick={() => void handleSignIn()}
                disabled={busy}
                className="rounded-xl bg-sky-500 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-sky-400 disabled:cursor-not-allowed disabled:opacity-60"
              >
                Sign In
              </button>
            </div>
          ) : (
            <div className="mt-4 grid gap-3 md:grid-cols-2">
              <input
                value={registerUsername}
                onChange={(event) => setRegisterUsername(event.target.value)}
                placeholder="Username"
                className="rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-slate-100"
              />
              <select
                value={registerRole}
                onChange={(event) => setRegisterRole(event.target.value)}
                className="rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-slate-100"
              >
                {roleOptions.map((role) => (
                  <option key={role} value={role}>
                    {role}
                  </option>
                ))}
              </select>
              <input
                value={registerEmail}
                onChange={(event) => setRegisterEmail(event.target.value)}
                placeholder="Email"
                className="rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-slate-100"
              />
              <input
                type="password"
                value={registerPassword}
                onChange={(event) => setRegisterPassword(event.target.value)}
                placeholder="Password"
                className="rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-slate-100"
              />
              <button
                onClick={() => void handleRegister()}
                disabled={busy}
                className="rounded-xl bg-emerald-500 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-emerald-400 disabled:cursor-not-allowed disabled:opacity-60"
              >
                Create Account
              </button>
            </div>
          )}
        </div>
      )}

      {error ? <p className="mt-4 rounded-xl border border-rose-700 bg-rose-950/60 p-3 text-sm text-rose-200">{error}</p> : null}
    </section>
  );
};

export default AuthPanel;

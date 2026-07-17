import { setAuthToken } from "../api/client";
import type { Member } from "../api/types";

const STORAGE_KEY = "folio.auth";

export const testMember: Member = {
  id: "m1",
  workspaceId: "w1",
  name: "Ada Lovelace",
  email: "ada@acme.test",
  role: "Owner",
};

/** Seed a signed-in session into localStorage + the API client for a test. */
export function seedSession(member: Member = testMember, token = "test-token") {
  localStorage.setItem(STORAGE_KEY, JSON.stringify({ token, member }));
  setAuthToken(token);
}

/** Clear any session left behind between tests. */
export function clearSession() {
  localStorage.removeItem(STORAGE_KEY);
  setAuthToken(null);
}

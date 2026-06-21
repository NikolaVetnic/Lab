import * as signalR from "@microsoft/signalr";
import type { ChatUser } from "./types";
import { createUser } from "./api";

export const handleCreateUserExternal = async (
  username: string,
  email: string,
  setCurrentUser: (user: ChatUser) => void,
) => {
  const createdUser = await createUser({
    username,
    email,
  });

  setCurrentUser(createdUser);
};

export const handleSendMessageExternal = async (
  currentUser: ChatUser | null,
  messageText: string,
  setMessageText: (text: string) => void,
  connection: signalR.HubConnection | null,
) => {
  if (!currentUser) {
    alert("Create a user first.");
    return;
  }

  if (!messageText.trim()) {
    return;
  }

  if (
    !connection ||
    connection.state !== signalR.HubConnectionState.Connected
  ) {
    alert("SignalR connection is not active.");
    return;
  }

  await connection.invoke("SendMessage", {
    senderId: currentUser.id,
    text: messageText,
  });

  setMessageText("");
};

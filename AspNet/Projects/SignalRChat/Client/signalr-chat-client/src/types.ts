export type ChatUser = {
  id: string;
  username: string;
  email: string;
};

export type ChatMessage = {
  id: string;
  senderId: string;
  senderUsername: string;
  text: string;
  sentAtUtc: string;
};

export type CreateUserRequest = {
  username: string;
  email: string;
};

export type SendMessageRequest = {
  senderId: string;
  text: string;
};

export type ActiveUsersResponse = {
  activeUserCount: number;
  users: ChatUser[];
};
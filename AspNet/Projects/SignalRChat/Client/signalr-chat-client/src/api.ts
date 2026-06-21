import type { ActiveUsersResponse, ChatMessage, ChatUser, CreateUserRequest } from './types';

const apiUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export async function createUser(
    request: CreateUserRequest
): Promise<ChatUser> {
    const response = await fetch(`${apiUrl}/users`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        throw new Error(`Error creating user: ${response.statusText}`);
    }

    return response.json();
}

export async function getMessages(): Promise<ChatMessage[]> {
    const response = await fetch(`${apiUrl}/messages`);

    if (!response.ok) {
        throw new Error(`Error fetching messages: ${response.statusText}`);
    }

    return response.json();
}

export async function getActiveUsers(): Promise<ChatUser[]> {
    const response = await fetch(`${apiUrl}/users/active`);

    if (!response.ok) {
        throw new Error(`Error fetching active users: ${response.statusText}`);
    }

    const data: ActiveUsersResponse = await response.json();

    console.log("Active-users API response:", data);

    return data.users;
}
import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { getMessages, getActiveUsers } from "./api";
import type { ChatMessage, ChatUser } from "./types";
import "./App.css";
import { handleCreateUserExternal, handleSendMessageExternal } from "./utils";

const signalRChatHubUrl = import.meta.env.VITE_SIGNALR_HUB_URL;

function App() {
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");

  const [currentUser, setCurrentUser] = useState<ChatUser | null>(null);
  const [activeUsers, setActiveUsers] = useState<ChatUser[]>([]);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [messageText, setMessageText] = useState("");

  const [connectionStatus, setConnectionStatus] = useState("Disconnected");
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    let disposed = false;

    async function loadMessageHistory() {
      try {
        const messageHistory = await getMessages();

        if (!disposed) {
          setMessages(messageHistory);
        }
      } catch (error) {
        if (!disposed) {
          console.error("Failed to load message history:", error);
        }
      }
    }

    loadMessageHistory();

    return () => {
      disposed = true;
    };
  }, []);

  useEffect(() => {
    if (!currentUser) {
      return;
    }

    let disposed = false;

    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(
        `${signalRChatHubUrl}?userId=${encodeURIComponent(currentUser.id)}`,
      )
      .withAutomaticReconnect()
      .build();

    connectionRef.current = newConnection;

    newConnection.on("ReceiveMessage", (message: ChatMessage) => {
      if (!disposed) {
        setMessages((previousMessages) => [...previousMessages, message]);
      }
    });

    newConnection.on("UserConnected", (user: ChatUser) => {
      setActiveUsers((existingUsers) => {
        const alreadyExists = existingUsers.some(
          (existingUser) => existingUser.id === user.id,
        );

        return alreadyExists ? existingUsers : [...existingUsers, user];
      });
    });

    newConnection.on("UserDisconnected", (userId: string) => {
      setActiveUsers((existingUsers) =>
        existingUsers.filter((user) => user.id !== userId),
      );
    });

    newConnection.onreconnecting(() => {
      if (!disposed) {
        setConnectionStatus("Reconnecting...");
      }
    });

    newConnection.onreconnected(() => {
      if (!disposed) {
        setConnectionStatus("Connected");
      }
    });

    newConnection.onclose(() => {
      if (!disposed) {
        setConnectionStatus("Disconnected");
      }
    });

    const startConnection = async () => {
      try {
        await newConnection.start();

        const users = await getActiveUsers();

        if (disposed) {
          return;
        }

        setActiveUsers(users);

        setConnectionStatus("Connected");
      } catch (error) {
        if (!disposed) {
          console.error("SignalR connection failed:", error);
          setConnectionStatus("Connection failed");
        }
      }
    };

    void startConnection();

    return () => {
      disposed = true;

      if (connectionRef.current === newConnection) {
        connectionRef.current = null;
      }

      void newConnection.stop();
    };
  }, [currentUser]);

  const handleCreateUser = handleCreateUserExternal.bind(
    null,
    username,
    email,
    setCurrentUser,
  );

  const handleSendMessage = () => {
    void handleSendMessageExternal(
      currentUser,
      messageText,
      setMessageText,
      connectionRef.current,
    );
  };

  const header = (
    <header className="chat-header">
      <div>
        <h1>SignalR Chat</h1>
        <p>Status: {connectionStatus}</p>
      </div>

      {currentUser && (
        <div className="current-user">
          Signed in as <strong>{currentUser.username}</strong>
        </div>
      )}
    </header>
  );

  const registerOrLoginForm = (
    <section className="user-form">
      <h2>Register / Login</h2>

      <input
        value={username}
        onChange={(event) => setUsername(event.target.value)}
        placeholder="Username"
      />

      <input
        value={email}
        onChange={(event) => setEmail(event.target.value)}
        placeholder="Email"
      />

      <button onClick={handleCreateUser}>Enter the chat</button>
    </section>
  );

  const chatMessages = (
    <section className="messages">
      {messages.map((message) => (
        <article
          key={message.id}
          className={
            message.senderId === currentUser?.id
              ? "message own-message"
              : "message"
          }
        >
          <div className="message-meta">
            <strong>{message.senderUsername}</strong>
            <span>{new Date(message.sentAtUtc).toLocaleTimeString()}</span>
          </div>

          <p>{message.text}</p>
        </article>
      ))}
    </section>
  );

  const chatSidebar = (
    <section className="sidebar">
      <ul>
        {activeUsers
          .slice()
          .sort((a, b) => a.username.localeCompare(b.username))
          .map((user) => (
            <li key={user.id}>{user.username}</li>
          ))}
      </ul>
    </section>
  );

  const chatWindow = (
    <div className={`main-window${currentUser ? " with-sidebar" : ""}`}>
      {chatMessages}
      {currentUser && chatSidebar}
    </div>
  );

  const sendMessageForm = (
    <section className="message-form">
      <input
        value={messageText}
        onChange={(event) => setMessageText(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            handleSendMessage();
          }
        }}
        placeholder="Write a message..."
        disabled={!currentUser}
      />

      <button onClick={handleSendMessage} disabled={!currentUser}>
        Send
      </button>
    </section>
  );

  return (
    <main className="page">
      <section className="chat-card">
        {header}
        {!currentUser && registerOrLoginForm}
        {chatWindow}
        {sendMessageForm}
      </section>
    </main>
  );
}

export default App;

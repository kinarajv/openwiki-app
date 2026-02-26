import { useState, useEffect } from "react";
import * as signalR from "@microsoft/signalr";
import { getBackendUrl } from "../config/api";

export function useSignalR(onReceiveChunk: (chunk: string) => void, onReceiveComplete: () => void) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${getBackendUrl()}/chatHub`)
      .withAutomaticReconnect()
      .build();
    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => {
          connection.on("ReceiveChunk", onReceiveChunk);
          connection.on("ReceiveComplete", onReceiveComplete);
        })
        .catch(e => console.log("SignalR failed:", e));
    }
    return () => {
      if (connection) {
        connection.off("ReceiveChunk");
        connection.off("ReceiveComplete");
      }
    };
  }, [connection, onReceiveChunk, onReceiveComplete]);

  const sendQuestion = async (repo: string, mode: string, question: string) => {
    if (connection && question.trim() && repo) {
      await connection.send("AskQuestion", repo, mode.toLowerCase(), question);
    }
  };

  return { connection, sendQuestion };
}

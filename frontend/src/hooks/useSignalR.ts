import { useState, useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { getBackendUrl } from "../config/api";

export function useSignalR(onReceiveChunk: (chunk: string) => void, onReceiveComplete: () => void) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  const onChunkRef = useRef(onReceiveChunk);
  const onCompleteRef = useRef(onReceiveComplete);

  useEffect(() => { onChunkRef.current = onReceiveChunk; }, [onReceiveChunk]);
  useEffect(() => { onCompleteRef.current = onReceiveComplete; }, [onReceiveComplete]);
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
          connection.on("ReceiveChunk", (chunk: string) => onChunkRef.current(chunk));
          connection.on("ReceiveComplete", () => onCompleteRef.current());
        })
        .catch(e => console.log("SignalR failed:", e));
    }
    return () => {
      if (connection) {
        connection.off("ReceiveChunk");
        connection.off("ReceiveComplete");
      }
    };
  }, [connection]);

  const sendQuestion = async (repo: string, mode: string, question: string) => {
    if (connection && question.trim() && repo) {
      await connection.send("AskQuestion", repo, mode.toLowerCase(), question);
    }
  };

  return { connection, sendQuestion };
}

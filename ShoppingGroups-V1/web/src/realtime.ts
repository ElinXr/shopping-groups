// ASP.NET Core SignalR JSON Hub Protocol over WebSocket, with skipNegotiation.
// No client-invokable hub methods are exposed; the server only sends invalidations.
export const RECORD_SEPARATOR = "\u001e";
export function decodeFrames(data: string): unknown[] {
  return data
    .split(RECORD_SEPARATOR)
    .filter(Boolean)
    .map((frame) => JSON.parse(frame));
}
export function realtime(changed: () => void, state: (connected: boolean) => void) {
  let socket: WebSocket | undefined;
  let stopped = false;
  let attempts = 0;
  let retry: ReturnType<typeof setTimeout>;
  let ping: ReturnType<typeof setInterval>;
  function connect() {
    if (stopped) return;
    socket = new WebSocket(
      `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/hubs/events`,
    );
    socket.onopen = () =>
      socket?.send(JSON.stringify({ protocol: "json", version: 1 }) + RECORD_SEPARATOR);
    socket.onmessage = (event) => {
      try {
        for (const raw of decodeFrames(String(event.data))) {
          const frame = raw as { type?: number; error?: string; target?: string };
          if (frame.error || frame.type === 7) {
            socket?.close();
            return;
          }
          if (frame.type === undefined) {
            attempts = 0;
            state(true);
            changed();
            clearInterval(ping);
            ping = setInterval(() => {
              if (socket?.readyState === WebSocket.OPEN)
                socket.send('{"type":6}' + RECORD_SEPARATOR);
            }, 15000);
          }
          if (frame.type === 1 && frame.target === "Changed") changed();
        }
      } catch {
        socket?.close();
      }
    };
    socket.onclose = () => {
      clearInterval(ping);
      state(false);
      if (!stopped) retry = setTimeout(connect, Math.min(30000, 1000 * 2 ** attempts++));
    };
    socket.onerror = () => socket?.close();
  }
  connect();
  return () => {
    stopped = true;
    clearTimeout(retry);
    clearInterval(ping);
    socket?.close();
  };
}

let isEnabled = false;

chrome.action.onClicked.addListener(function (tab) {
  isEnabled = !isEnabled;
  changeStatusLabel();
});

chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  changeStatusLabel();
});

function changeStatusLabel() {
  if (isEnabled) {
    chrome.action.setBadgeBackgroundColor({ color: "green" });
    chrome.action.setBadgeTextColor({ color: "white" });
    chrome.action.setBadgeText({
      text: "",
    });
  } else {
    chrome.action.setBadgeBackgroundColor({ color: "orange" });
    chrome.action.setBadgeTextColor({ color: "black" });
    chrome.action.setBadgeText({
      text: "off",
    });
  }
}

// WebSocket client
class WebSocketClient {
  constructor(
    url,
    clientId,
    reconnectInterval = 5000,
    maxReconnectAttempts = 0,
    logging = false
  ) {
    this.webSocketUrl = url;
    this.clientId = clientId;
    this.reconnectInterval = reconnectInterval;
    this.maxReconnectAttempts = maxReconnectAttempts;
    this.reconnectAttempts = 0;
    this.isConnected = false;
    this.socket = null;
    this.isReconnecting = false;
    this.logging = logging;

    // Event handlers (optional)
    this.onConnectionEstablished = null;
    this.onConnectionLost = null;
    this.onMessageReceived = null;
    this.onErrorOccurred = null;
  }

  connect() {
    if (this.isConnected) {
      if (this.logging) console.log("WebSocket is already connected.");
      return;
    }

    if (this.isReconnecting) {
      if (this.logging) console.log("WebSocket is trying to reconnect.");
      return;
    }

    this.isReconnecting = true;
    try {
      this.socket = new WebSocket(
        `${this.webSocketUrl}/ws?clientId=${this.clientId}`
      );
    } catch (e) {
      if (this.logging) console.error(`[error] ${e.message}`);
      this.isReconnecting = false;
      return;
    }

    this.socket.onopen = (e) => this.onOpen(e);
    this.socket.onmessage = (event) => this.onMessage(event);
    this.socket.onclose = (event) => this.onClose(event);
    this.socket.onerror = (error) => this.onError(error);
  }

  onOpen(event) {
    if (this.logging) console.log("[open] Connection established");
    this.reconnectAttempts = 0;
    this.isConnected = true;
    this.isReconnecting = false;

    // Invoke custom handler for connection established, if provided
    if (typeof this.onConnectionEstablished === "function") {
      this.onConnectionEstablished();
    }

    // Example: start sending messages to the server every 5 seconds
    this.sendMessages();
  }

  onMessage(event) {
    if (this.logging)
      console.log(`[message] Data received from server: ${event.data}`);

    // Invoke custom handler for message received, if provided
    if (typeof this.onMessageReceived === "function") {
      this.onMessageReceived(event.data);
    }
  }

  onClose(event) {
    this.isConnected = false;
    this.isReconnecting = false;

    if (event.wasClean) {
      if (this.logging)
        console.log(
          `[close] Connection closed cleanly, code=${event.code}, reason=${event.reason}`
        );
    } else {
      if (this.logging)
        console.log("[close] Connection lost. Attempting to reconnect...");

      // Invoke custom handler for connection lost, if provided
      if (typeof this.onConnectionLost === "function") {
        this.onConnectionLost();
      }

      this.attemptReconnect();
    }
  }

  onError(error) {
    if (this.logging) console.error(`[error] ${error.message}`);
    this.isConnected = false;
    this.isReconnecting = false;

    // Invoke custom handler for error, if provided
    if (typeof this.onErrorOccurred === "function") {
      this.onErrorOccurred(error);
    }

    this.attemptReconnect();
  }

  async attemptReconnect() {
    if (
      this.maxReconnectAttempts === 0 ||
      this.reconnectAttempts < this.maxReconnectAttempts
    ) {
      this.reconnectAttempts++;
      if (this.logging)
        console.log(`Attempt ${this.reconnectAttempts} to reconnect...`);
      await this.sleep(this.reconnectInterval);
      this.connect();
    } else {
      if (this.logging)
        console.log(
          "Max reconnect attempts reached. Please check the server status."
        );
    }
  }

  sendMessages() {
    if (this.socket.readyState === WebSocket.OPEN) {
      const content = `Message from client ${
        this.clientId
      } at ${new Date().toISOString()}`;

      const message = JSON.stringify({
        refId: "00000000-0000-0000-0002-000000000001",
        content: content
      });

      if (this.logging) console.log(`Sending: ${message}`);
      this.socket.send(message);
    }

    setTimeout(() => {
      if (this.isConnected) {
        this.sendMessages();
      }
    }, 5000);
  }

  send(message) {
    if (this.isConnected && this.socket.readyState === WebSocket.OPEN) {
      if (this.logging) console.log(`Sending: ${message}`);
      this.socket.send(message);
    } else {
      if (this.logging)
        console.warn("Cannot send message, WebSocket is not connected.");
    }
  }

  disconnect() {
    if (this.isConnected) {
      this.socket.close(1000, "Client closing connection.");
      this.isConnected = false;
      this.isReconnecting = false;
    }
  }

  sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  // Setters for event handlers
  setOnConnectionEstablished(handler) {
    this.onConnectionEstablished = handler;
  }

  setOnConnectionLost(handler) {
    this.onConnectionLost = handler;
  }

  setOnMessageReceived(handler) {
    this.onMessageReceived = handler;
  }

  setOnErrorOccurred(handler) {
    this.onErrorOccurred = handler;
  }
}

// Example usage
const webSocketClient = new WebSocketClient(
  "wss://localhost:7199",
  "00000000-0000-0000-0001-000000000001"
);

webSocketClient.setOnConnectionEstablished(() => {
  console.log("EventHandler: Connection established!");
});

webSocketClient.setOnConnectionLost(() => {
  console.log("EventHandler: Connection lost!");
});

webSocketClient.setOnMessageReceived((message) => {
  console.log("EventHandler: Received a message from the server:", message);
});

webSocketClient.setOnErrorOccurred((error) => {
  console.error("EventHandler: WebSocket error occurred:", error);
});

// Start connection
webSocketClient.connect();

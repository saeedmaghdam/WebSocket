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
const webSocketUrl = "wss://localhost:7199";

let socket;
let reconnectInterval = 5000; // Time to wait before attempting to reconnect
let maxReconnectAttempts = 0;
let reconnectAttempts = 0;
const clientId = '00000000-0000-0000-0000-000000000000'; // Client ID (empty GUID)

function connectWebSocket() {
    socket = new WebSocket(`${webSocketUrl}/ws?clientId=${clientId}`);

    socket.onopen = function(e) {
        console.log("[open] Connection established");
        reconnectAttempts = 0; // Reset the reconnection attempts counter

        // Start sending messages to the server every second
        setInterval(() => {
            if (socket.readyState === WebSocket.OPEN) {
                const message = `Message from client ${clientId} at ${new Date().toISOString()}`;
                console.log(`Sending: ${message}`);
                socket.send(message);
            }
        }, 5000);
    };

    socket.onmessage = function(event) {
        console.log(`[message] Data received from server: ${event.data}`);
    };

    socket.onclose = function(event) {
        if (event.wasClean) {
            console.log(`[close] Connection closed cleanly, code=${event.code}, reason=${event.reason}`);
        } else {
            console.log('[close] Connection lost. Attempting to reconnect...');
            attemptReconnect();
        }
    };

    socket.onerror = function(error) {
        console.error(`[error] ${error.message}`);
        attemptReconnect();
    };
}

function attemptReconnect() {
    if (maxReconnectAttempts == 0 || reconnectAttempts < maxReconnectAttempts) {
        setTimeout(() => {
            reconnectAttempts++;
            console.log(`Attempt ${reconnectAttempts} to reconnect...`);
            connectWebSocket();
        }, reconnectInterval);
    } else {
        console.log('Max reconnect attempts reached. Please check the server status.');
    }
}

// Start the WebSocket connection
connectWebSocket();
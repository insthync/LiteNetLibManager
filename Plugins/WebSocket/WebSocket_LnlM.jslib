var LibraryWebSockets_LnlM = {
	$webSocketInstances_LnlM: [],

	// Create instance and connect to server
	// Events: DataEvent = 0, ConnectEvent = 1, DisconnectEvent = 2, ErrorEvent = 3
	SocketCreate_LnlM: function(url)
	{
		var str = UTF8ToString(url);
		var socket = {
			socket: new WebSocket(str),
			buffer: new Uint8Array(0),
			events: []
		}

		socket.socket.binaryType = 'arraybuffer';

		socket.socket.onopen = function(e) {
			socket.events.push({
				type: 1,
			});
		};

		socket.socket.onmessage = function(e) {
			// TODO: handle other data types?
			if (e.data instanceof Blob)
			{
				var reader = new FileReader();
				reader.addEventListener("loadend", function() {
					socket.events.push({
						type: 0,
						data: new Uint8Array(reader.result)
					});
				});
				reader.readAsArrayBuffer(e.data);
			}
			else if (e.data instanceof ArrayBuffer)
			{
				socket.events.push({
					type: 0,
					data: new Uint8Array(e.data)
				});
			}
		};

		socket.socket.onerror = function(e) {
			// Ref: https://developer.mozilla.org/en-US/docs/Web/API/CloseEvent/code
			socket.events.push({
				type: 3,
				code: e.code,
				reason: e.reason,
			});
		};

		socket.socket.onclose = function(e) {
			// Ref: https://developer.mozilla.org/en-US/docs/Web/API/CloseEvent/code
			socket.events.push({
				type: 2,
				code: e.code,
				reason: e.reason,
			});
		};

		// Return instance pointer
		var instance = webSocketInstances_LnlM.push(socket) - 1;
		return instance;
	},

	// Get connection state
	GetSocketState_LnlM: function (socketInstance)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		// Ref: https://developer.mozilla.org/en-US/docs/Web/API/WebSocket/readyState
		if (!socket)
			return 3;
		return socket.socket.readyState;
	},

	// Get latest event type
	GetSocketEventType_LnlM: function(socketInstance)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		if (!socket || socket.events.length == 0)
			return -1;
		return socket.events[0].type;
	},

	// Get latest error code
	GetSocketErrorCode_LnlM: function(socketInstance)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		if (!socket || socket.events.length == 0 || (socket.events[0].type != 3 && socket.events[0].type != 2))
			return 0;
		return socket.events[0].code;
	},

	// Get latest data length
	GetSocketDataLength_LnlM: function(socketInstance)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		if (!socket || socket.events.length == 0 || socket.events[0].type != 0 || socket.events[0].data.length == 0)
			return 0;
		return socket.events[0].data.length;
	},

	// Get latest data byte array
	GetSocketData_LnlM: function(socketInstance, ptr, length)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		if (!socket || socket.events.length == 0 || socket.events[0].type != 0 || socket.events[0].data.length == 0 || socket.events[0].data.length > length)
			return;
		HEAPU8.set(socket.events[0].data, ptr);
	},

	// Dequeue network event
	SocketEventDequeue_LnlM: function(socketInstance)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		if (!socket || socket.events.length == 0)
			return;
		socket.events = socket.events.slice(1);
	},

	// Send message
	SocketSend_LnlM: function (socketInstance, ptr, length)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		if (!socket)
			return;
		socket.socket.send(HEAPU8.buffer.slice(ptr, ptr + length));
	},

	// Close connection
	SocketClose_LnlM: function (socketInstance)
	{
		var socket = webSocketInstances_LnlM[socketInstance];
		if (!socket)
			return;
		socket.socket.close();
	}
};

autoAddDeps(LibraryWebSockets_LnlM, '$webSocketInstances_LnlM');
mergeInto(LibraryManager.library, LibraryWebSockets_LnlM);

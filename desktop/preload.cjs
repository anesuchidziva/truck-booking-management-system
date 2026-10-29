const { contextBridge } = require('electron');

contextBridge.exposeInMainWorld('amcottsDesktop', {
  productName: 'Amcotts Workshop Control',
  version: '1.0.0'
});

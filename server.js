const net = require('net')
const { Readable } = require('stream')
const server = net.createServer(socket => {
    const source = new Readable({
        read(_size) {
            this.push(Uint8Array.of(randomUint8(), randomUint8(), 0x0d, 0x0a))
        }
    })
    source.pipe(socket)
    socket.on('end', () => {
        console.log('end')
        source.destroy()
    })
    socket.on('close', () => {
        console.log('close')
        source.unpipe(socket)
    })
})
server.listen(502)

function randomUint8() {
    return Math.floor(Math.random() * 256)
}
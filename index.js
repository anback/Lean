let request = require('request')
let fs = require('fs')
let moment = require('moment')
let {Transform} = require('stream')
let zlib = require('zlib')
let archiver = require('archiver')
var progress = require('stream-progressbar');
let format = 'YYYYMMDD'
let QUOTE = 'quote'
let TRADE = 'trade'

let getUri = ({date, type}) => `https://s3-eu-west-1.amazonaws.com/public.bitmex.com/data/${type}/${date}.csv.gz` // 20180101
let getPath = ({date, type}) => `./data/crypto/bitmex/tick/xbtusd/${date}_${type}.zip`

// input: 2018-09-01D04:03:41.128828000,XBTUSD,585783,7063.5,7064,142932
let mapQuote = ([timestamp, symbol, bidSize, bidPrice, askPrice, askSize]) =>
  [moment(timestamp).valueOf() - moment(timestamp).startOf('day').valueOf(), bidPrice, bidSize, askPrice, askSize].join(',')

// input: 2018-01-22D00:00:02.364320000,XBTUSD,Sell,20,11559.5,MinusTick,046e0b31-267d-007b-179e-aa8e8fd31c69,173020,0.0017302,20
let mapTrade = ([timestamp, symbol, side, amount, price, tickType, matchId, numb1, numb2]) =>
  [moment(timestamp).valueOf() - moment(timestamp).startOf('day').valueOf(), price, amount].join(',')

if (!fs.existsSync('./data/crypto/bitmex')) fs.mkdirSync('./data/crypto/bitmex')
if (!fs.existsSync('./data/crypto/bitmex/tick')) fs.mkdirSync('./data/crypto/bitmex/tick')
if (!fs.existsSync('./data/crypto/bitmex/tick/xbtusd')) fs.mkdirSync('./data/crypto/bitmex/tick/xbtusd')

let startDate = moment('2018-09-04').startOf('day')
let endDate = moment('2018-09-04').startOf('day')
let date = startDate
let dates = []
while (date.valueOf() <= endDate.valueOf()) { dates.push(date.format(format)); date.add(1, 'd')}

let createTransform = (options) => new Transform({
    transform: function transformer(chunk, encoding, callback) {
        let {type} = options
        let rows = chunk.toString('utf8').split('\n')

        rows =
        rows.map(row => {
          let data = row.split(',')
          if(data[1] !== 'XBTUSD') return
          data[0] = data[0].replace('D', 'T')
          if(type === TRADE) return mapTrade(data, options)
          if(type === QUOTE) return mapQuote(data, options)
        })
        .filter(r => !!r)

        this.push(rows.join('\n'))
        callback()
    }
  });

[QUOTE].forEach(type =>
  dates
  // .filter(date => !fs.existsSync(getPath({date, type})))
  .map(date => {
    let path = getPath({date, type})
    let stream =
    request(getUri({date, type}))
    .pipe(progress(`${path} [:bar] :rate/bps :percent :etas`))
    .pipe(zlib.createGunzip())
    .pipe(createTransform({date, type}))

    var archive = archiver('zip',   {zlib: { level: 9 } });
    archive.pipe(fs.createWriteStream(path))
    archive.append(stream, { name: `${date}.csv` })
    archive.finalize();
  }))

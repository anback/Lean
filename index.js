let request = require('request')
let fs = require('fs')
let moment = require('moment')
let {Transform} = require('stream')
let zlib = require('zlib')
let path = './data/bitmex'
let format = 'YYYYMMDD'
let quote = 'quote'
let trade = 'trade'

let getUri = (date, type) => `https://s3-eu-west-1.amazonaws.com/public.bitmex.com/data/${type}/${date}.csv.gz` // 20180101
let getPath = (date, type) => `${path}/${type}/${date}.csv.gz`

// input: 2018-09-01D04:03:41.128828000,XBTUSD,585783,7063.5,7064,142932
// output:
let mapQuote = (quote) => 

if (!fs.existsSync('./data/bitmex')) fs.mkdirSync('./data/bitmex')
if (!fs.existsSync('./data/bitmex/quote')) fs.mkdirSync('./data/bitmex/quote')
if (!fs.existsSync('./data/bitmex/trade')) fs.mkdirSync('./data/bitmex/trade')

let startDate = moment('2018-09-01')
let endDate = moment('2018-09-02')
let date = startDate
let dates = []
while (date.valueOf() < endDate.valueOf()) { dates.push(date.format(format)); date.add(1, 'd')}


let logStream = new Transform({
    transform: function transformer(chunk, encoding, callback){
        let [timestamp, symbol, bidSize, bidPrice, askPrice, askSize] = chunk.toString('utf8').split(',')
        if(symbol !== 'XBTUSD') return
        callback(false, chunk);
    }
  });

[quote].forEach(type =>
  dates
  .filter(date => !fs.existsSync(getPath(date, type)))
  .map(date => {
    request(getUri(date, type))
    .pipe(zlib.createGunzip())
    .pipe(logStream)
    .pipe(fs.createWriteStream(getPath(date, type)))
  }))

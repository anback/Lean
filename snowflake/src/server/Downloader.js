//@flow
import request from 'request';
import fs from 'fs'
import moment from 'moment'
import {Transform} from 'stream'
import zlib from 'zlib'
import archiver from 'archiver'
import progress from 'stream-progressbar'
import QuoteAggregator from './QuoteAggregator';
import TradeAggregator from './TradeAggregator'
import {ONE_SECOND, ONE_MINUTE, ONE_HOUR, ONE_DAY} from './Consts'
import split from 'split'

const FORMAT = 'YYYYMMDD'
const QUOTE = 'quote'
const TRADE = 'trade'
const TICK = 'tick'

const RESOLUTIONS = [
  {name: 'second', value: ONE_SECOND},
  {name: 'minute', value: ONE_MINUTE},
  {name: 'hour', value: ONE_HOUR},
  {name: 'daily', value: ONE_DAY}
]

const TICK_RESOLUTION = {name: TICK, value: 0}
const ALL_RESOLUTIONS = [...RESOLUTIONS, TICK_RESOLUTION]

let getUri = ({date, type}) => `https://s3-eu-west-1.amazonaws.com/public.bitmex.com/data/${type}/${date}.csv.gz` // 20180101
let getPath = ({date, type, resolution}) => `../data/crypto/gdax/${resolution.name}/xbtusd/${date}_${type}.zip`
let onError = (e, options: Object) => {
  let path = getPath(options)
  if(fs.existsSync(path)) fs.unlinkSync(path)
  console.error(e)
}

// input: 2018-09-01D04:03:41.128828000,XBTUSD,585783,7063.5,7064,142932
let mapQuote = ([timestamp, symbol, bidSize, bidPrice, askPrice, askSize]) =>
  [moment(timestamp).valueOf() - moment(timestamp).startOf('day').valueOf(), bidPrice, bidSize, askPrice, askSize]

// input: 2018-01-22D00:00:02.364320000,XBTUSD,Sell,20,11559.5,MinusTick,046e0b31-267d-007b-179e-aa8e8fd31c69,173020,0.0017302,20
let mapTrade = ([timestamp, symbol, side, amount, price, tickType, matchId, numb1, numb2]) =>
  [moment(timestamp).valueOf() - moment(timestamp).startOf('day').valueOf(), price, amount, side]

if (!fs.existsSync('../data/crypto/gdax')) fs.mkdirSync('../data/crypto/gdax')
if (!fs.existsSync('../data/crypto/gdax/tick')) fs.mkdirSync('../data/crypto/gdax/tick')
if (!fs.existsSync('../data/crypto/gdax/tick/xbtusd')) fs.mkdirSync('../data/crypto/gdax/tick/xbtusd')
if (!fs.existsSync('../data/crypto/gdax/second')) fs.mkdirSync('../data/crypto/gdax/second')
if (!fs.existsSync('../data/crypto/gdax/second/xbtusd')) fs.mkdirSync('../data/crypto/gdax/second/xbtusd')
if (!fs.existsSync('../data/crypto/gdax/minute')) fs.mkdirSync('../data/crypto/gdax/minute')
if (!fs.existsSync('../data/crypto/gdax/minute/xbtusd')) fs.mkdirSync('../data/crypto/gdax/minute/xbtusd')
if (!fs.existsSync('../data/crypto/gdax/hour')) fs.mkdirSync('../data/crypto/gdax/hour')
if (!fs.existsSync('../data/crypto/gdax/hour/xbtusd')) fs.mkdirSync('../data/crypto/gdax/hour/xbtusd')
if (!fs.existsSync('../data/crypto/gdax/daily')) fs.mkdirSync('../data/crypto/gdax/daily')
if (!fs.existsSync('../data/crypto/gdax/daily/xbtusd')) fs.mkdirSync('../data/crypto/gdax/daily/xbtusd')

let {argv} = process
let startDate = '20180901'
let endDate = moment().add(-1, 'day').startOf('day').format(FORMAT)

if(argv.length === 3) {startDate = argv[2]}
if(argv.length === 4) {startDate = argv[2]; endDate = argv[3]}

let date = moment(startDate)
let dates = []

while (date.valueOf() <= moment(endDate).valueOf()) { dates.push(date.format(FORMAT)); date.add(1, 'd')}

process.on('SIGINT', e => {
  [TRADE, QUOTE].forEach(type => dates.forEach(date => ALL_RESOLUTIONS.forEach(resolution => {
    try { fs.unlinkSync(getPath({type, date, resolution})) } catch (e) {}
  })))
  process.exit(1)
})

let mapDataTransformFactory = (options) => new Transform({
    transform: function transformer(line, encoding, callback) {
        let {type, tradeAggregators, quoteAggregators} = options
        line = line.toString('utf8')
        let data = line.split(',')
        if(data[1] !== 'XBTUSD') return callback()
        data[0] = data[0].replace('D', 'T')
        if(type === TRADE) data = mapTrade(data)
        if(type === QUOTE) data = mapQuote(data)

        if(type === TRADE) tradeAggregators.forEach((aggregator) => aggregator.onNewTrade(data, options))
        if(type === QUOTE) quoteAggregators.forEach((aggregator) => aggregator.onNewTrade(data, options))
        this.push(data.join(',') + '\n')
        callback()
    }
  });

let aggregateDataTransformFactory = (options) => new Transform({
    transform: function transformer(line, encoding, callback) {
      let data
      try {
        let {type} = options
        line = line.toString('utf8').replace('\n', '')
        data = line.split(',')
        data = data.map(str => !isNaN(str) ? parseFloat(str) : str) //try parse str

        switch(true) {
          case !this.data: this.data = data; return callback()
          case type === TRADE && shouldAggregateTrade(data, this.data): this.data = aggregateTrade(data, this.data); return callback();
          case type === QUOTE && shouldAggregateQuote(data, this.data): this.data = aggregateQuote(data, this.data); return callback();
          default:
            this.push(this.data.join(',') + '\n')
            this.data = data
            callback()
        }
      }
      catch (e) {
        console.log('data', data)
        console.log('this.data', this.data)
        throw e
      }
    }
  });

let shouldAggregateQuote = ([time, bidPrice, bidSize, askPrice, askSize], [_time, _bidPrice, _bidSize, _askPrice, _askSize]) => bidPrice === _bidPrice && askPrice === _askPrice
let shouldAggregateTrade = ([time, price, amount, side], [_time, _price, _amount, _side]) => time === _time && side === _side && price === _price
let aggregateTrade = ([time, price, amount, side], [_time, _price, _amount, _side]) => [_time, _price, amount + _amount, _side]
let aggregateQuote = ([time, bidPrice, bidSize, askPrice, askSize], [_time, _bidPrice, _bidSize, _askPrice, _askSize]) => [_time, bidPrice, bidSize, askPrice, askSize]

;[QUOTE, TRADE].forEach(type =>
  dates
  .filter(date => !fs.existsSync(getPath({date, type, resolution: TICK_RESOLUTION})))
  .forEach(date => {
    let options = {date, type, resolution: TICK_RESOLUTION}
    let path = getPath({date, type, resolution: TICK_RESOLUTION})
    let tradeAggregators = RESOLUTIONS.map(resolution => new TradeAggregator(resolution))
    let quoteAggregators = RESOLUTIONS.map(resolution => new QuoteAggregator(resolution))

    let uri = getUri({date, type})
    console.log(`GET ${uri}`)
    let stream =
    request(uri, (error, response) => {
      if(error) return onError(error, options)
      if(response.statusCode !== 200) return onError(new Error(JSON.stringify(response)), options)
    })
    .pipe(progress(`${path} [:bar] :rate/bps :percent :etas`))
    .pipe(zlib.createGunzip())
    .pipe(split())
    .pipe(mapDataTransformFactory({date, type, tradeAggregators, quoteAggregators}))
    .pipe(aggregateDataTransformFactory({type}))

    var archive = archiver('zip',   {zlib: { level: 9 } });
    let output = fs.createWriteStream(path)
    archive.pipe(output)
    archive.append(stream, { name: `${date}.csv` })
    archive.finalize()

    output.on('error', e => onError(e, options))
    stream.on('error', e => onError(e, options))

    let aggregators
    if(type === TRADE) aggregators = tradeAggregators
    if(type === QUOTE) aggregators = quoteAggregators

    output.on('close', () => aggregators.forEach(aggregator => {
      let path = getPath({date, type, resolution: aggregator.resolution})
      var archive = archiver('zip',   {zlib: { level: 9 } });
      let text = aggregator.getBars().map(bar => bar.join(',')).join('\n')
      archive.pipe(fs.createWriteStream(path))
      archive.append(text, { name: `${date}.csv` })
      archive.finalize()
    }))
  }))

// @flow
import moment from 'moment'
const ONE_SECOND = 1000
const TEN_MILLION = 10000000
const SOMETHING_HIGH = TEN_MILLION

let BARS = {}

let getKeyFromTimestamp = (timestamp: number):string => `${Math.floor(timestamp / ONE_SECOND) * ONE_SECOND}`

export default (trade: Object) => {
  let key = getKeyFromTimestamp(moment(trade.timestamp).valueOf())
  let bar = BARS[key] || {minBidPrice: SOMETHING_HIGH, maxAskPrice: 0, avgBidSize: 0, avgAskSize: 0, spread: 0, power: 0}

  bar.maxAskPrice = Math.max(bar.maxAskPrice, quote.askPrice)
  bar.minBidPrice = Math.min(bar.minBidPrice, quote.bidPrice)
  bar.avgBidSize = Math.round(bar.avgBidSize ? (bar.avgBidSize + quote.bidSize) / 2 : quote.bidSize)
  bar.avgAskSize = Math.round(bar.avgBidSize ? (bar.avgAskSize + quote.askSize) / 2 : quote.askSize)
  bar.spread = round(bar.maxAskPrice - bar.minBidPrice, 1)
  bar.power = round((bar.avgBidSize - bar.avgAskSize) / bar.avgBidSize, 2)

  this.bars[key] = bar
}

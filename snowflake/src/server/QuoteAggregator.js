// @flow
type Bar = {time: number, openBidPrice: number, highBidPrice: number, lowBidPrice: number, closeBidPrice: number, bidVolume: number, openAskPrice: number, highAskPrice: number, lowAskPrice: number, closeAskPrice: number, askVolume: number}

const TEN_MILLION = 10000000
const SOMETHING_HIGH = TEN_MILLION

export default class {
  resolution: Resolution
  bars: Object
  bars = {}
  constructor(resolution: Resolution) { this.resolution = resolution }

  onNewTrade = ([time, bidPrice, bidSize, askPrice, askSize]: Array<number>, {type, date}: Object) => {

    let key = this.getKey(time)
    let bar = this.bars[key] || {time: 0, openBidPrice: 0, highBidPrice: 0, lowBidPrice: SOMETHING_HIGH, closeBidPrice: 0, bidVolume: 0, openAskPrice: 0, highAskPrice: 0, lowAskPrice: SOMETHING_HIGH, closeAskPrice: 0, askVolume: 0}
    bar = (bar: Bar)

    bidPrice = parseFloat(bidPrice)
    bidSize = parseFloat(bidSize)
    askPrice = parseFloat(askPrice)
    askSize = parseFloat(askSize)

    bar.time = key

    bar.openBidPrice = bar.openBidPrice || bidPrice
    bar.highBidPrice = Math.max(bar.highBidPrice, bidPrice)
    bar.lowBidPrice = Math.min(bar.lowBidPrice, bidPrice)
    bar.closeBidPrice = bidPrice
    bar.bidVolume = bidSize

    bar.openAskPrice = bar.openAskPrice || askPrice
    bar.highAskPrice = Math.max(bar.highAskPrice, askPrice)
    bar.lowAskPrice = Math.min(bar.lowAskPrice, askPrice)
    bar.closeAskPrice = askPrice
    bar.askVolume = askSize

    if(!bar.openBidPrice) console.log(bidPrice, bidSize, date, type)
    if(!bar.highBidPrice) console.log(bidPrice, bidSize, date, type)
    if(!bar.lowBidPrice) console.log(bidPrice, bidSize, date, type)

    if(!bar.openAskPrice) console.log(askPrice, askSize, date, type)
    if(!bar.highAskPrice) console.log(askPrice, askSize, date, type)
    if(!bar.lowAskPrice) console.log(askPrice, askSize, date, type)

    this.bars[key] = bar
  }

  getKey = (time: number):number => Math.floor(time / this.resolution.value) * this.resolution.value

  getBars = (): Array<Array<number>> => Object.keys(this.bars).map(key => this.bars[key]).map(bar => [bar.time, bar.openBidPrice, bar.highBidPrice, bar.lowBidPrice, bar.closeBidPrice, bar.bidVolume, bar.openAskPrice, bar.highAskPrice, bar.lowAskPrice, bar.closeAskPrice, bar.askVolume])
}

let request = require('request')
let fs = require('fs')
let moment = require('moment')
let path = './data/bitmex'
let format = 'YYYYMMDD'
let quote = 'quote'
let trade = 'trade'

let getUri = (date, type) => `https://s3-eu-west-1.amazonaws.com/public.bitmex.com/data/${type}/${date}.csv.gz` // 20180101
let getPath = (date, type) => `${path}/${type}/${date}.csv.gz`

if (!fs.existsSync('./data/bitmex')) fs.mkdirSync('./data/bitmex')
if (!fs.existsSync('./data/bitmex/quote')) fs.mkdirSync('./data/bitmex/quote')
if (!fs.existsSync('./data/bitmex/trade')) fs.mkdirSync('./data/bitmex/trade')

let startDate = moment('2018-08-01')
let endDate = moment('2018-09-02')
let date = startDate
let dates = []
while (date.valueOf() < endDate.valueOf()) { dates.push(date.format(format)); date.add(1, 'd')};

[quote,trade].forEach(type =>
  dates
  .filter(date => !fs.existsSync(getPath(date, type)))
  .map(date => {
    request(getUri(date, type)).pipe(fs.createWriteStream(getPath(date, type)))
    request(getUri(date, type)).pipe(fs.createWriteStream(getPath(date, type)))
  }))

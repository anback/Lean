import React from "react";
import PropTypes from "prop-types";

import { format } from "d3-format";
import { timeFormat } from "d3-time-format";

import { ChartCanvas, Chart, ZoomButtons } from "react-stockcharts";
import {
	BarSeries,
	CandlestickSeries,
	LineSeries
} from "react-stockcharts/lib/series";
import { XAxis, YAxis } from "react-stockcharts/lib/axes";
import {
	CrossHairCursor,
	MouseCoordinateX,
	MouseCoordinateY,
} from "react-stockcharts/lib/coordinates";

import { discontinuousTimeScaleProvider } from "react-stockcharts/lib/scale";
import {
	OHLCTooltip,
} from "react-stockcharts/lib/tooltip";
import { fitWidth } from "react-stockcharts/lib/helper";
import { last } from "react-stockcharts/lib/utils";

class CandleStickChartWithZoomPan extends React.Component {
	constructor(props) {
		super(props);
		this.saveNode = this.saveNode.bind(this);
		this.resetYDomain = this.resetYDomain.bind(this);
		this.handleReset = this.handleReset.bind(this);
	}
	componentWillMount() {
		this.setState({
			suffix: 1
		});
	}
	saveNode(node) {
		this.node = node;
	}
	resetYDomain() {
		this.node.resetYDomain();
	}
	handleReset() {
		this.setState({
			suffix: this.state.suffix + 1
		});
	}
	render() {
		const { type, width, ratio } = this.props;
		const { mouseMoveEvent, panEvent, zoomEvent, zoomAnchor } = this.props;
		const { clamp } = this.props;

		const { data: initialData } = this.props;

		const xScaleProvider = discontinuousTimeScaleProvider
			.inputDateAccessor(d => d.date);
		const {
			data,
			xScale,
			xAccessor,
			displayXAccessor,
		} = xScaleProvider(initialData);

		const start = xAccessor(last(data));
		const end = xAccessor(data[Math.max(0, data.length - 150)]);
		const xExtents = [start, end];

		const margin = { left: 70, right: 70, top: 20, bottom: 30 };

		const height = 1120;

		const gridHeight = height - margin.top - margin.bottom;
		const gridWidth = width - margin.left - margin.right;

		const showGrid = true;
		const yGrid = showGrid ? { innerTickSize: -1 * gridWidth, tickStrokeOpacity: 0.2 } : {};
		const xGrid = showGrid ? { innerTickSize: -1 * gridHeight, tickStrokeOpacity: 0.2 } : {};

		return (
			<ChartCanvas
				ref={this.saveNode}
				height={height}
				ratio={ratio}
				width={width}
				margin={{ left: 70, right: 70, top: 10, bottom: 30 }}

				mouseMoveEvent={mouseMoveEvent}
				panEvent={panEvent}
				zoomEvent={zoomEvent}
				clamp={clamp}
				zoomAnchor={zoomAnchor}
				type={type}
				seriesName={`MSFT_${this.state.suffix}`}
				data={data}
				xScale={xScale}
				xExtents={xExtents}
				xAccessor={xAccessor}
				displayXAccessor={displayXAccessor}>
				{this.getCharts().map(({height, getComponent}, i, array) => getComponent({height, id: i+1, top: array.filter((x, j) => j >= i).reduce((a,b) => a + b.height, 0)}))}
				<CrossHairCursor />
			</ChartCanvas>
		);
	}

	getCharts() {

		const { width } = this.props;
		const { zoomEvent } = this.props;

		const margin = { left: 70, right: 70, top: 20, bottom: 30 };

		const height = 1120;

		const gridHeight = height - margin.top - margin.bottom;
		const gridWidth = width - margin.left - margin.right;

		const showGrid = true;
		const yGrid = showGrid ? { innerTickSize: -1 * gridWidth, tickStrokeOpacity: 0.2 } : {};
		const xGrid = showGrid ? { innerTickSize: -1 * gridHeight, tickStrokeOpacity: 0.2 } : {};

		return [{
			height: 400,
			getComponent: ({height, id}) => <Chart id={id}
				yExtents={d => [d.high, d.low]}
				height={height}>
				<YAxis axisAt="right" orient="right" ticks={5} zoomEnabled={zoomEvent} {...yGrid} />

				<MouseCoordinateY
					at="right"
					orient="right"
					displayFormat={format(".2f")} />

				<CandlestickSeries />
				<OHLCTooltip origin={[-40, 0]}/>
				<ZoomButtons
					onReset={this.handleReset}
				/>
			</Chart>
		},
		{
			height: 150,
			getComponent: ({height, top, id}) => {
				return <Chart id={id} yExtents={d => d.volume} height={height} origin={(w, h) => [0, h - top]}>
					<YAxis axisAt="left" orient="left" ticks={5} tickFormat={format(".2s")} zoomEnabled={zoomEvent} />

					<MouseCoordinateY at="left" orient="left" displayFormat={format(".4s")} />

					<BarSeries yAccessor={d => d.volume} fill={(d) => d.close > d.open ? "#6BA583" : "#FF0000"} />
				</Chart>
			}
		},
		{
			height: 150,
			getComponent : ({height, top, id}) => <Chart id={id} yExtents={d => d.atr14} height={height} origin={(w, h) => [0, h - top]}>
				<XAxis axisAt="bottom" orient="bottom" zoomEnabled={zoomEvent} {...xGrid} />
				<YAxis axisAt="left" orient="left" ticks={5} tickFormat={format(".2s")} zoomEnabled={zoomEvent} />

				<MouseCoordinateX at="bottom" orient="bottom" displayFormat={timeFormat("%Y-%m-%d %H:%M:%S")} />
				<MouseCoordinateY at="left" orient="left" displayFormat={format(".4s")} />

				<LineSeries yAccessor={d => d.atr14} />
			</Chart>
		},
		{
			height: 150,
			getComponent : ({height, top, id}) => <Chart id={id} yExtents={d => d.orderflow} height={height} origin={(w, h) => [0, h - top]}>
				<XAxis axisAt="bottom" orient="bottom" zoomEnabled={zoomEvent} {...xGrid} />
				<YAxis axisAt="left" orient="left" ticks={5} tickFormat={format(".2s")} zoomEnabled={zoomEvent} />

				<MouseCoordinateX at="bottom" orient="bottom" displayFormat={timeFormat("%Y-%m-%d %H:%M:%S")} />
				<MouseCoordinateY at="left" orient="left" displayFormat={format(".4s")} />

				<LineSeries yAccessor={d => d.orderflow} />
			</Chart>
		},
		{
			height: 150,
			getComponent: ({height, top, id}) => <Chart id={id} yExtents={(d: DataRow) => Math.sign(Math.random() - 0.5) * 200} height={height} origin={(w, h) => [0, h - top]}>
				<YAxis axisAt="left" orient="left" ticks={5} tickFormat={format(".2s")} zoomEnabled={zoomEvent} />

				<MouseCoordinateX at="bottom" orient="bottom" displayFormat={timeFormat("%Y-%m-%d %H:%M:%S")} />
				<MouseCoordinateY at="left" orient="left" displayFormat={format(".4s")} />

				<BarSeries
					yAccessor={d => d.backtestValue}
					fill={(d) => d.backtestValue > 0 ? "#6BA583" : "#FF0000"}
					baseAt={(xScale, yScale, d) => yScale(0)} />
			</Chart>
		},
		{
			height: 150,
			getComponent : ({height, top, id}) => <Chart id={id} yExtents={(d: DataRow) => [0, -25000]} height={height} origin={(w, h) => [0, h - top]}>
				<XAxis axisAt="bottom" orient="bottom" zoomEnabled={zoomEvent} {...xGrid} />
				<YAxis axisAt="left" orient="left" ticks={5} tickFormat={format(".2s")} zoomEnabled={zoomEvent} />

				<MouseCoordinateX at="bottom" orient="bottom" displayFormat={timeFormat("%Y-%m-%d %H:%M:%S")} />
				<MouseCoordinateY at="left" orient="left" displayFormat={format(".4s")} />

				<LineSeries
					yAccessor={d => d.backtestResult} />
			</Chart>
		}]
	}
}

CandleStickChartWithZoomPan.propTypes = {
	data: PropTypes.array.isRequired,
	width: PropTypes.number.isRequired,
	ratio: PropTypes.number.isRequired,
	type: PropTypes.oneOf(["svg", "hybrid"]).isRequired,
};

CandleStickChartWithZoomPan.defaultProps = {
	type: "svg",
	mouseMoveEvent: true,
	panEvent: true,
	zoomEvent: true,
	clamp: false,
};

CandleStickChartWithZoomPan = fitWidth(CandleStickChartWithZoomPan);

export default CandleStickChartWithZoomPan;

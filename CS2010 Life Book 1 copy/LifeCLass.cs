using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//      10        20        30        40        50        60        70        80        90       100
//34567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890
// okay ke 12/18/2017

namespace CS_Life_Console_v5
{
   public sealed class GameOfLife : IDisposable
   {
    // member private const fields
    private const int _LIFE_SIZE = _XSIZE * _YSIZE;
    private const int _MASK = _LIFE_SIZE - 1;
    private const int _NEIGHBOR_EE = 1 + _LIFE_SIZE;
    private const int _NEIGHBOR_NE = -_XSIZE + 1 + _LIFE_SIZE;
    private const int _NEIGHBOR_NN = -_XSIZE + _LIFE_SIZE;
    private const int _NEIGHBOR_NW = -_XSIZE - 1 + _LIFE_SIZE;
    private const int _NEIGHBOR_SE = _XSIZE + 1 + _LIFE_SIZE;
    private const int _NEIGHBOR_SS = _XSIZE + _LIFE_SIZE;
    private const int _NEIGHBOR_SW = _XSIZE - 1 + _LIFE_SIZE;
    private const int _NEIGHBOR_WW = -1 + _LIFE_SIZE;
    private const int _NS_TO_SEC = 1000000;
    private const int _TEST_CELL_END = -1;
    private const int _WINDOW_HEIGHT = 26;
    private const int _WINDOW_WIDTH = 80;
    private const int _XSIZE = 1024;
    private const int _XSIZE_MINUS_WINDOW_WIDTH = _XSIZE - 1 - _WINDOW_WIDTH;
    private const int _YSIZE = _XSIZE;
    private const int _YSIZE_MINUS_WINDOW_HEIGHT = _YSIZE - 1 - _WINDOW_HEIGHT;

    // private objects
    private readonly StringBuilder _displayString = new StringBuilder();
    private readonly Stopwatch _drawTimer = new Stopwatch();
    private readonly Random _randomLocation = new Random(_LIFE_SIZE);
    private readonly Stopwatch _stepTimer = new Stopwatch();
    private readonly long _stopwatchFreq = Stopwatch.Frequency;
    private readonly Task _updateCellsTask;

    // private arrays
    private readonly long[] _livingAvg = new long[10];
    private readonly int[] _nextTestCells = new int[_LIFE_SIZE];
    private readonly int[] _testCells = new int[_LIFE_SIZE];

    private byte[] _lastMap = new byte[_LIFE_SIZE];
    private byte[] _swapMap = new byte[_LIFE_SIZE];
    private readonly byte[] _testMap = new byte[_LIFE_SIZE];
    private byte[] _workMap = new byte[_LIFE_SIZE];

    // private mutable
    private int _aliveThisStep = 0;
    private bool _allCellUpdate = true;
    private int _cellAddPtr = 0;
    private LMode _currentMode = LMode.STEP;
    private int _displayViewX = 0;
    private int _displayViewY = 0;
    private bool _disposed = false;
    private int _livesAccum = 0;
    private int _neighborCount = 0;
    private long _nsPerStep = 0;
    private long _ptr = 0;
    private long _runningSum = 0;
    private int _stepsTaken = 1;

    static GameOfLife() => LifeClass_s = new GameOfLife();

    public static GameOfLife LifeClass_s { get; }

    ~GameOfLife() => Dispose(false);

    private GameOfLife()
    {
      _displayViewX = (_XSIZE - _WINDOW_WIDTH) / 2;                 //start col location to display
      _displayViewY = (_YSIZE - _WINDOW_HEIGHT) / 2;                 //start row location to display
      _updateCellsTask = new Task(lifeLoopTask);
      _updateCellsTask.Start();                   // start the calc thread
      _nextTestCells[0] = _TEST_CELL_END;                        // set list end
    }

    internal void Dispose(bool disposing)
    {
      if (!_disposed)
      {
        _disposed = true;
        if (disposing)
        { GC.SuppressFinalize(this); }
      }
      LifeClass_s.Dispose();
    }

    public void ChangeViewBy(int x, int y)
    {
      int tempX = _displayViewX + x;
      tempX = tempX >= (_XSIZE_MINUS_WINDOW_WIDTH) ? _XSIZE_MINUS_WINDOW_WIDTH : tempX;
      _displayViewX = tempX <= 0 ? 0 : tempX;

      int tempY = _displayViewY + y;
      _displayViewY = tempY >= (_YSIZE_MINUS_WINDOW_HEIGHT) ? _YSIZE_MINUS_WINDOW_HEIGHT : tempY;
      _displayViewY = tempY <= 0 ? 0 : _displayViewY;
    }

    public void Dispose()
    {
      if (!_disposed)
      {
        Dispose(true);
      }
    }

    public StringBuilder DrawView()
    {
      int dx = 0;
      int dy = 0;

      _displayString.Clear();
      _drawTimer.Restart();

      for (int y = 0; y <= (_WINDOW_HEIGHT - 1); y++)
      {
        dy = (y + _displayViewY) * _XSIZE;                    // calc offest xn a one dxm array based on  xy

        for (int x = 0; x <= (_WINDOW_WIDTH - 1); x++)
        {
          dx = _displayViewX + dy + x;                     //calc offest xnto the array for the col data
          _displayString.Append(_workMap[dx] == 1 ? "#" : " ");
        }
        _displayString.AppendLine();                // carraxge return for the end of the cols
      }

      _drawTimer.Stop();

      _displayString.AppendFormat($"" +
          $"{_stepsTaken,6} " +
          $"Screen={_displayViewX,3},{_displayViewY,3} " +
          $"lives={_aliveThisStep,4} " +
          $"ns/calc={_nsPerStep,4} " +
          $"ms/draw{_drawTimer.ElapsedTicks * _NS_TO_SEC / _stopwatchFreq,4}" +
          $" QGLSR_+CAWDX " +
          $"|{NextMode}|"); //add bottom row

      return _displayString;
    }

    public LMode NextMode { get; set; } = LMode.ADD_SMALL;

    private void addTests(int cell)
    {
      _livesAccum++;
      addToTestMap(cell);
      addToTestMap((cell + _NEIGHBOR_NW) & _MASK);
      addToTestMap((cell + _NEIGHBOR_NN) & _MASK);
      addToTestMap((cell + _NEIGHBOR_NE) & _MASK);
      addToTestMap((cell + _NEIGHBOR_WW) & _MASK);
      addToTestMap((cell + _NEIGHBOR_EE) & _MASK);
      addToTestMap((cell + _NEIGHBOR_SW) & _MASK);
      addToTestMap((cell + _NEIGHBOR_SS) & _MASK);
      addToTestMap((cell + _NEIGHBOR_SE) & _MASK);
    }

    private void addToMapViewCentered(int row, int col)
    {
      int dx = (_XSIZE / 2) + row;                        // local var - lower_case
      int dy = ((_YSIZE / 2) + col) * _XSIZE;
      _workMap[dx + dy] = 1;
      _lastMap[dx + dy] = 1;
    }

    private void addToTestMap(int cell)
    {
      if (_testMap[cell] == 0)
      {
        _testMap[cell] = 1;
        _nextTestCells[_cellAddPtr] = cell;
        _cellAddPtr++;
      }
    }

    private void averageStepTime()
    {
      _ptr = (_ptr + 1) % 9;

      _runningSum -= _livingAvg[_ptr];
      _livingAvg[_ptr] = _stepTimer.ElapsedTicks * _NS_TO_SEC / _stopwatchFreq;
      _runningSum += _livingAvg[_ptr];
      _nsPerStep = _runningSum / 10;
    }

    private void changeMode(LMode newMode)
    {
      int nx;
      const int FIN = _LIFE_SIZE / 50;
      int index;

      switch (newMode)
      {
        case LMode.CLEAR:       // clear
          Array.Clear(_workMap, 0, _LIFE_SIZE);
          Array.Clear(_lastMap, 0, _LIFE_SIZE);
          _stepsTaken = 0;
          break;

        case LMode.AUTO:
        case LMode.EXIT:
          break;

        case LMode.IDLE:
          Thread.Sleep(50);               // sleep the thread for 50ms (only in manual mode)
          break;

        case LMode.ADD_SMALL:             // S = 7 point starter
          addToMapViewCentered(1, 0);
          addToMapViewCentered(3, 1);
          addToMapViewCentered(0, 2);
          addToMapViewCentered(1, 2);
          addToMapViewCentered(4, 2);
          addToMapViewCentered(5, 2);
          addToMapViewCentered(6, 2);
          NextMode = LMode.IDLE;
          break;

        case LMode.ADD_GLIDER:
          addToMapViewCentered(3, 0);
          addToMapViewCentered(3, 1);
          addToMapViewCentered(3, 2);
          addToMapViewCentered(2, 0);
          addToMapViewCentered(1, 1);
          NextMode = LMode.IDLE;
          break;

        case LMode.ADD_LARGE:              // L point starter
          addToMapViewCentered(0, 0);
          addToMapViewCentered(2, 0);
          addToMapViewCentered(2, 1);
          addToMapViewCentered(4, 2);
          addToMapViewCentered(4, 3);

          addToMapViewCentered(4, 4);
          addToMapViewCentered(6, 3);
          addToMapViewCentered(6, 4);
          addToMapViewCentered(6, 5);
          addToMapViewCentered(7, 4);
          NextMode = LMode.IDLE;
          break;

        case LMode.ADD_RANDOM:            // R = random fill of entire row/col

          for (index = 0; index < FIN; ++index)
          {                                               // use a for to iterate the randoms
            nx = _randomLocation.Next(0, _LIFE_SIZE);    // create a new random number winthin the array
            _lastMap[nx] = 1;                             //write the array with a 1, add a cell
            _workMap[nx] = 1;
          }
          NextMode = LMode.IDLE;
          break;
      }
      _currentMode = newMode;
      _allCellUpdate = true;
    }

    private void checkAllCells()
    {
      _cellAddPtr = 0;
      _livesAccum = 0;
      int nextCell = 0;

      if (_allCellUpdate)               // test all cells
      {
        while (nextCell < _LIFE_SIZE)
        {
          countNeighbors(nextCell);
          nextCell++;
        }
      }
      else
      {                                 // test only testcells
        while (_testCells[nextCell] != _TEST_CELL_END)
        {
          countNeighbors(_testCells[nextCell]);
          nextCell++;
        }
      }

      _allCellUpdate = false;
      _aliveThisStep = _livesAccum;
      _nextTestCells[_cellAddPtr] = _TEST_CELL_END;
    }

    private void countNeighbors(int cell)
    {
      _neighborCount = _lastMap[(cell + _NEIGHBOR_NW) & _MASK];
      _neighborCount += _lastMap[(cell + _NEIGHBOR_NN) & _MASK];
      _neighborCount += _lastMap[(cell + _NEIGHBOR_NE) & _MASK];
      _neighborCount += _lastMap[(cell + _NEIGHBOR_WW) & _MASK];
      _neighborCount += _lastMap[(cell + _NEIGHBOR_EE) & _MASK];
      _neighborCount += _lastMap[(cell + _NEIGHBOR_SW) & _MASK];
      _neighborCount += _lastMap[(cell + _NEIGHBOR_SS) & _MASK];
      _neighborCount += _lastMap[(cell + _NEIGHBOR_SE) & _MASK];

      switch (_neighborCount)
      {                                 // switch case on the number of cells alive around our test cell
        case 2:
          _workMap[cell] = _lastMap[cell];
          break;

        case 3:                         // if three it is always alive
          _workMap[cell] = 1;
          break;

        default:                        // if other then dead
          _workMap[cell] = 0;
          break;
      }

      if (_workMap[cell] == 1)
      {
        addTests(cell);
      }
    }

    private void lifeLoopTask()
    {
      do
      {
        if (NextMode == LMode.AUTO)
        {                                      // if we are not in single step mode
          nextStep();                         // update a step
        }

        if (NextMode == LMode.STEP)
        {                                      // if we are in single step mode
          _allCellUpdate = true;
          nextStep();                         // update a tick
          NextMode = LMode.IDLE;              // clear flag so we only do it once
        }

        if (NextMode != _currentMode)
        {
          changeMode(NextMode);
        }
      } while (NextMode != LMode.EXIT);         // while is allways true, thread never ends on its own
    }

    private void nextStep()
    {
      _stepTimer.Restart();

      for (int i = 0; i < _cellAddPtr; ++i)
      {
        _testMap[_nextTestCells[i]] = 0;       // reset testboard
      }
      _swapMap = _lastMap;                      // swap boards
      _lastMap = _workMap;
      _workMap = _swapMap;

      Array.Clear(_workMap, 0, _LIFE_SIZE);
      Array.Copy(_nextTestCells, _testCells, _cellAddPtr + 1);

      checkAllCells();

      _stepTimer.Stop();                        // x64 237ns
      _stepsTaken++;
      averageStepTime();
    }
  }
}
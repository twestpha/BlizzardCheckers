using System;
using System.Threading;
using System.Collections.Generic;

//##############################################################################
// Overall comments:
//##############################################################################
// Controls: up-down-right-left arrows to move, space to select, esc to quit
//
// I thought this was going to be a lot simpler, but it grew to need things
// like a dedicated position struct with basic functionality. I probably spent
// the most time on the player input, the core game logic for validating
// and making moves, and the stalemate detection.
//
// I liked how clean the ascii representation turned out, and I was able to add
// some extra art and instructions for the player, as well as the cursor
// selection system, which makes it more interactive and playable. The
// alternative, 'typing in pairs of numbers for a move' felt really flat and
// not interactive. Part of my focus here was not just programming the rules
// but also keeping in mind user interaction - like the thread sleeping during
// the enemy move. For a program like this, it's not about performance, it's
// about player interactivity and comprehension.
//
// I also considered making a version where each tile is an object that
// contains a list of valid moves that could be made while on it - this seemed
// to have legs up until committing a large move with several jumps and walking
// a large number of other tiles backward to update all their valid moves and
// also remove jumped pieces. This was a problem I didn't have a solution to.
//  However, that  version would have made detecting stalemates much easier,
// but it would add difficult-to-maintain complexity and only benefited the
// stalemate detection.

//##############################################################################
// Position struct to contain position references on the board
//##############################################################################
struct Position {
    public int x, y;

    public Position(int x_,  int y_){
        x = x_;
        y = y_;
    }

    public static Position operator+(Position a, Position b){
        return new Position(a.x + b.x, a.y + b.y);
    }

    public static bool Equals(Position a, Position b){
        return a.x == b.x && a.y == b.y;
    }
}

//##############################################################################
// Board class to contain piece placement, legality of moves, and displaying
//##############################################################################
class Board {
    class PossibleJump {
        public PossibleJump previous;
        public Position from;
        public Position to;
        public Position jumpedPiece;
    }

    public enum BoardPiece : int {
        xPiece,
        oPiece,
        empty,
    }

    char[] BoardPieceSymbols = {
        'X',
        'O',
        '.'
    };

    public const int BOARD_WIDTH = 8;
    public const int BOARD_HEIGHT = 8;

    public const int BOARD_X_SIDE = 3;
    public const int BOARD_O_SIDE = 4;

    public const int INVALID_POSITION = -1;

    public const int MAX_PIECE_COUNT = 12;

    BoardPiece[,] peices;

    Position selectionMove;
    Position selectionPiece;
    bool useSelectionMove, useSelectionPiece;
    int oPieceCount, xPieceCount;

    public Board(){
        // Start at bottom left corner
        selectionMove.x = 0;
        selectionMove.y = BOARD_HEIGHT - 1;
        selectionPiece.x = 0;
        selectionPiece.y = BOARD_HEIGHT - 1;

        useSelectionMove = false;
        useSelectionPiece = false;

        oPieceCount = MAX_PIECE_COUNT;
        xPieceCount = MAX_PIECE_COUNT;

        peices = new BoardPiece[BOARD_WIDTH, BOARD_HEIGHT];

        // Fill out board in default state
        for(int y = 0; y < BOARD_HEIGHT; ++y){
            for(int x = 0; x < BOARD_WIDTH; ++x){
                bool yeven = y % 2 == 0;
                bool xeven = x % 2 == 0;

                if(y < BOARD_X_SIDE){
                    if(yeven != xeven){
                        peices[x, y] = BoardPiece.xPiece;
                    } else {
                        peices[x, y] = BoardPiece.empty;
                    }
                } else if(y > BOARD_O_SIDE){
                    if(yeven != xeven){
                        peices[x, y] = BoardPiece.oPiece;
                    } else {
                        peices[x, y] = BoardPiece.empty;
                    }
                } else {
                    peices[x, y] = BoardPiece.empty;
                }
            }
        }
    }

    public void PrintBoard(){
        String toPrint = "";
        char emptyPosition = BoardPieceSymbols[(int) BoardPiece.empty];

        Console.WriteLine("+-----------------+");
        for(int y = 0; y < BOARD_HEIGHT; ++y){
            toPrint = "|";

            // Print the entire row of peices, with empty spaces in between
            // Making sure to fill in selection and cursor pieces if they're active
            for(int x = 0; x < BOARD_WIDTH; ++x){
                string left = " ";

                if(useSelectionPiece && y == selectionPiece.y){
                    if(x == selectionPiece.x){
                        left = "[";
                    } else if(x == selectionPiece.x + 1){
                        left = "]";
                    }
                }

                if(useSelectionMove && y == selectionMove.y){
                    if(x == selectionMove.x){
                        left = "<";
                    } else if(x == selectionMove.x + 1){
                        left = ">";
                    }
                }

                toPrint += left + BoardPieceSymbols[(int) peices[x, y]];
            }

            if(useSelectionMove && y == selectionMove.y && selectionMove.x + 1 == BOARD_WIDTH){
                toPrint += ">|";
            } else if(useSelectionPiece && y == selectionPiece.y && selectionPiece.x + 1 == BOARD_WIDTH){
                toPrint += "]|";
            } else {
                toPrint += " |";
            }

            Console.WriteLine(toPrint);
        }
        Console.WriteLine("+-----------------+");
    }

    public bool TryMovePiece(Position from, Position to, bool commit){
        BoardPiece movedPiece = peices[from.x, from.y];

        // Can't move onto uneven spaces
        bool toXeven = to.x % 2 == 0;
        bool toYeven = to.y % 2 == 0;
        if(toXeven == toYeven){
            return false;
        }

        // Catch simple issues, like moving the wrong direction or empty spaces
        if(movedPiece == BoardPiece.empty){ return false; }
        if(movedPiece == BoardPiece.xPiece && from.y >= to.y){
            return false;
        } else if(movedPiece == BoardPiece.oPiece && from.y <= to.y){
            return false;
        }

        // Catch out of bounds errors
        if(to.x < 0 || to.x >= BOARD_WIDTH || to.y < 0 || to. y >= BOARD_HEIGHT){
            return false;
        }

        // Try for a "simple" move, 1 to the right or left and in the right direction
        int deltaX = to.x - from.x;
        int deltaY = to.y - from.y;

        if(Math.Abs(deltaX) == 1 && Math.Abs(deltaY) == 1 && PieceAtPosition(to) == BoardPiece.empty){
            if(commit){ MovePieceOnBoard(from, to); }
            return true;
        } else {
            bool foundJumps = false;
            int possibleIndex = 0;
            List<PossibleJump> possibleJumps = new List<PossibleJump>();
            Position current = from;

            int yDirection = movedPiece == BoardPiece.xPiece ? 1 : -1;

            while(current.y >= 0 && current.y < BOARD_HEIGHT){
                // Try both left & right jumps
                for(int i = 0; i < 2; ++i){
                    Position toTry = new Position(current.x + (i == 0 ? -2 : 2), current.y + (yDirection * 2));

                    if(toTry.x >= 0 && toTry.x < BOARD_WIDTH && toTry.y >= 0 && toTry.y < BOARD_HEIGHT){
                        // if valid, see if piece is empty and has jumpable piece there
                        bool toTryEmpty = PieceAtPosition(toTry) == BoardPiece.empty;

                        Position jumpedPosition = new Position(current.x + (i == 0 ? -1 : 1), current.y + yDirection);
                        BoardPiece jumpedPiece = PieceAtPosition(jumpedPosition);
                        bool opposites = OppositePiece(movedPiece, jumpedPiece);

                        // If so, save jump and move on to next possible jumps
                        if(toTryEmpty && opposites){
                            PossibleJump jump = new PossibleJump();

                            jump.from = current;
                            jump.to = toTry;
                            jump.jumpedPiece = jumpedPosition;

                            // iterate over all other possibleJumps, if they have a to that equals our from, fill out our previous
                            for(int j = 0; j < possibleJumps.Count; ++j){
                                if(Position.Equals(possibleJumps[j].to, jump.from)){
                                    jump.previous = possibleJumps[j];
                                }
                            }

                            possibleJumps.Add(jump);

                            if(Position.Equals(toTry, to)){
                                foundJumps = true;
                                break;
                            }
                        }
                    }
                }

                if(!foundJumps && possibleIndex < possibleJumps.Count){
                    current = possibleJumps[possibleIndex].to;
                    possibleIndex++;
                } else {
                    break;
                }
            }

            if(foundJumps){
                if(commit){
                    PossibleJump jump = possibleJumps[possibleJumps.Count - 1];

                    while(jump != null){
                        BoardPiece removedPiece = peices[jump.jumpedPiece.x, jump.jumpedPiece.y];
                        peices[jump.jumpedPiece.x, jump.jumpedPiece.y] = BoardPiece.empty;

                        if(removedPiece == BoardPiece.xPiece){
                            xPieceCount--;
                        } else {
                            oPieceCount--;
                        }

                        jump = jump.previous;
                    }

                    MovePieceOnBoard(from, to);
                }

                return true;
            }
        }

        return false;
    }

    private void MovePieceOnBoard(Position from, Position to){
        BoardPiece movedPiece = peices[from.x, from.y];
        peices[from.x, from.y] = BoardPiece.empty;
        peices[to.x, to.y] = movedPiece;
    }

    public void SetUseSelectionPiece(bool newUseSelection){
        useSelectionPiece = newUseSelection;
    }

    public void SetUseSelectionMove(bool newUseSelection){
        useSelectionMove = newUseSelection;
    }

    public void MoveSelectionPiece(Position newPosition){
        selectionPiece = newPosition;
        Clamp(ref selectionPiece.x, 0, BOARD_WIDTH - 1);
        Clamp(ref selectionPiece.y, 0, BOARD_HEIGHT - 1);
    }

    public Position GetSelectionPiece(){
        return selectionPiece;
    }

    public void MoveSelectionMove(Position newPosition){
        selectionMove = newPosition;
        Clamp(ref selectionMove.x, 0, BOARD_WIDTH - 1);
        Clamp(ref selectionMove.y, 0, BOARD_HEIGHT - 1);
    }

    public Position GetSelectionMove(){
        return selectionMove;
    }

    public void Clamp(ref int a, int x, int y){
        if(a < x){ a = x; return; }
        if(a > y){ a = y; return; }
    }

    public bool OppositePiece(BoardPiece a, BoardPiece b){
        return (a == BoardPiece.xPiece && b == BoardPiece.oPiece) || (a == BoardPiece.oPiece && b == BoardPiece.xPiece);
    }

    public BoardPiece PieceAtPosition(Position pos){
        return peices[pos.x, pos.y];
    }

    public bool IsPlayerPiece(Position pos){
        return PieceAtPosition(pos) == BoardPiece.oPiece;
    }

    public bool IsAIPiece(Position pos){
        return PieceAtPosition(pos) == BoardPiece.xPiece;
    }

    public bool IsLegalMovePossible(){
        // I don't like brute forcing this but my 4 hours is almost up
        // I also don't think this catches when one side can't move... but again, times up
        for(int y = 0; y < BOARD_HEIGHT; ++y){
            for(int x = 0; x < BOARD_WIDTH; ++x){
                // For each space, try every other space
                for(int yto = 0; yto < BOARD_HEIGHT; ++yto){
                    for(int xto = 0; xto < BOARD_WIDTH; ++xto){
                        if(TryMovePiece(new Position(x, y), new Position(xto, yto), false)){
                            return true;
                        }
                    }
                }
            }
        }

        // return false;

        return true;
    }

    public int GetPlayerPieceCount(){
        return oPieceCount;
    }

    public int GetAIPieceCount(){
        return xPieceCount;
    }
}

//##############################################################################
// Checkers Game class for handling player and ai, and managing turns
//##############################################################################
class CheckersGame {
    enum PlayerState {
        SelectingPiece,
        SelectingMove,
    }

    enum Winner {
        aiWin,
        playerWin,
        draw,
    }

    bool playerTurn;
    bool exit;
    bool illegalMoveMade;
    PlayerState playerState;
    Winner winner;
    Board board;

    public CheckersGame(){
        // Default start as player's turn
        playerTurn = true;
        exit = false;

        playerState = PlayerState.SelectingPiece;
        board = new Board();
    }

    public void Play(){
        PrintBoard();

        while(!IsFinished()){
            MakeMove();
            PrintBoard();
        }

        OutputWinner();
    }

    public bool IsFinished(){
        bool legalMove = board.IsLegalMovePossible();
        bool playerNoPieces = board.GetPlayerPieceCount() == 0;
        bool aiNoPieces = board.GetAIPieceCount() == 0;

        if(aiNoPieces){
            winner = Winner.playerWin;
        } else if(playerNoPieces) {
            winner = Winner.aiWin;
        } else if(!legalMove) {
            winner = Winner.draw;
        }

        return exit || !legalMove || playerNoPieces || aiNoPieces;
    }

    public void PrintBoard(){
        board.SetUseSelectionPiece(playerTurn);
        board.SetUseSelectionMove(playerTurn && playerState == PlayerState.SelectingMove);

        // Portable clear-screen
        Console.WriteLine("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n");

        // Some instructions, too "Select piece to move O", "Select position to move to", etc.
        Console.WriteLine("+-----------------+");
        if(playerTurn){
            if(illegalMoveMade){
                Console.WriteLine("| Illegal Move    |");
            } else if(playerState == PlayerState.SelectingPiece){
                Console.WriteLine("| Select O Piece  |");
            } else {
                Console.WriteLine("| Select Move     |");
            }
        } else {
            Console.WriteLine("| Enemy Move      |");
        }

        board.PrintBoard();
    }

    public void OutputWinner(){
        if(!exit){
            if(winner == Winner.aiWin){
                Console.WriteLine("| AI Won          |");
            } else if(winner == Winner.playerWin){
                Console.WriteLine("| Player Won!     |");
            } else {
                Console.WriteLine("| Game Stalemate  |");
            }
        } else {
            Console.WriteLine("| Game Quit       |");
        }
        Console.WriteLine("+-----------------+");
    }

    public void MakeMove(){
        bool madeValidMove = playerTurn ? MakePlayerMove() : MakeAIMove();

        if(madeValidMove){
            playerTurn = !playerTurn;
        }
    }

    private bool MakeAIMove(){
        // Artificially delay the move so the player has time to see what's happening
        Thread.Sleep(2000);

        // Then, get a list of all the ai pieces
        int aiCount = 0;
        Position[] aiPositions = new Position[Board.MAX_PIECE_COUNT];
        for(int x = 0; x < Board.BOARD_WIDTH; ++x){
            for(int y = 0; y < Board.BOARD_HEIGHT; ++y){
                Position pos = new Position(x, y);
                if(board.IsAIPiece(pos)){
                    aiPositions[aiCount] = pos;
                    aiCount++;
                }
            }
        }

        // Then, pick a random start index
        Random rand = new Random();
        int startIndex = rand.Next(aiCount);

        bool validMove = false;
        while(!validMove){
            // Iterate backward over x and y, if found a valid move, take it
            Position currentAiPosition = aiPositions[startIndex];

            for(int y = Board.BOARD_HEIGHT; y > currentAiPosition.y; --y){
                for(int x = 0; x < Board.BOARD_WIDTH; ++x){
                    if(board.TryMovePiece(currentAiPosition, new Position(x, y), true)){
                        return true;
                    }
                }
            }

            startIndex = (startIndex + 1) % aiCount;
        }

        return false;
    }

    private bool MakePlayerMove(){
        // Convert Keypresses into Actions
        Position cursorMove;
        cursorMove.x = 0;
        cursorMove.y = 0;

        ConsoleKey key = Console.ReadKey().Key;

        if(key == ConsoleKey.Escape){
            exit = true;
        }

        if(key == ConsoleKey.UpArrow){
            cursorMove.y = -1; // Y is reversed like most screens
        } else if(key == ConsoleKey.DownArrow){
            cursorMove.y = 1;
        } else if(key == ConsoleKey.RightArrow){
            cursorMove.x = 1;
        } else if(key == ConsoleKey.LeftArrow){
            cursorMove.x = -1;
        }

        if(cursorMove.x != 0 || cursorMove.y != 0){
            if(playerState == PlayerState.SelectingPiece){
                board.MoveSelectionPiece(board.GetSelectionPiece() + cursorMove);
            } else {
                board.MoveSelectionMove(board.GetSelectionMove() + cursorMove);
            }
        }

        if(key == ConsoleKey.Spacebar){
            illegalMoveMade = false;

            if(playerState == PlayerState.SelectingPiece && board.IsPlayerPiece(board.GetSelectionPiece())){
                // Honestly, we could make this "if selected our piece and that piece has a valid move",
                // but that takes away from the perception of player freedom
                playerState = PlayerState.SelectingMove;
                board.MoveSelectionMove(board.GetSelectionPiece());
            } else if(playerState == PlayerState.SelectingMove){
                playerState = PlayerState.SelectingPiece;

                if(board.TryMovePiece(board.GetSelectionPiece(), board.GetSelectionMove(), true)){
                    return true;
                } else {
                    illegalMoveMade = true;
                }
            }
        }

        return false;
    }
}

//##############################################################################
// Main program boots up game and plays it
//##############################################################################
class Program {
    static void Main(string[] args){
        CheckersGame game = new CheckersGame();
        game.Play();
    }
}

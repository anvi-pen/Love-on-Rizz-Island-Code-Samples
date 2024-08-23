using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/*
This script manages the card matching minigame. There are 16 cards (8 pairs) that are
flipped over and shuffled. The player and the NPC opponent (refered to as monster in
this script) take turns to each flip over two cards during their turn, with the goal 
of getting matching pairs of cards (matching cards will have the same picture on them). 
After all cards have been paired, the winner is determined by whoever matched the most 
pairs of cards.

The minigame always starts with the player's turn.

The monster keeps a memory of every card that has been flipped over and depicts a
picture that it has not seen before. Hence, there are two arrays: one array stores
the cards that the monster remembers the pictures of (cardsInMonMemory) and another
array stores the remaining cards that are not in the monster's memory (cards).
During the monster's turn, their first card is never chosen from those in its
memory. If the picture on the first card matches a picture they have seen before on
another card (which would be in cardsInMonMemory), there is a high likelihood that
the monster picks the matching card from their memory. By implementing this memory
system for the NPC monster, the difficulty of the minigame for the player is elevated.
*/

public class CardManager : MonoBehaviour
{
    // Internal variables and required components
    List<GameObject> cards = new List<GameObject>();
    Dictionary<int, Card> cardsInMonMemory = new Dictionary<int, Card>();

    enum CARDS_STATE
    {
        NO_CARD_UP,
        ONE_CARD_UP,
        TWO_CARD_UP
    }

    CARDS_STATE cardState = CARDS_STATE.NO_CARD_UP;
    Card firstCard = null;
    Card secondCard = null;

    private int pairsLeft = 8;

    private bool checkingTwoCards = false;

    [SerializeField] List<Sprite> cardSprites = new List<Sprite>();

    private bool isPlayerTurn = true;

    private Camera camera;

    private int playerMatches = 0;
    private int monsterMatches = 0;

    private MinigameController controller;

    // UI Canvas
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text monsterText;
    [SerializeField] private TMP_Text playerText;

    private void Awake()
    {
        camera = Camera.main;
    }

    // Start is called before the first frame update
    void Start()
    {
        controller = GameObject.Find("MinigameController").GetComponent<MinigameController>();

        // Store all 16 card game objects in an array
        foreach (Transform child in transform)
        {
            cards.Add(child.gameObject);
        }

        // Randomly select the picture to match on each card
            // This acts as a way to shuffle the 16 cards
        List<int> IDPool = new List<int> { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8 };
        foreach (GameObject card in cards)
        {
            int index = Random.Range(0, IDPool.Count);
            int id = IDPool[index];
            card.GetComponent<Card>().ID = id;
            card.GetComponent<Card>().Set_Initial_Sprite(cardSprites[id - 1]);
            IDPool.RemoveAt(index);
        }

        // Set necessary UI text
        turnText.SetText("Your Turn!");
        monsterText.SetText("Lilith's Score: " + monsterMatches);
        playerText.SetText("Your Score: " + playerMatches);
    }

    // Detect whether a card has been clicked by the player's mouse to be flipped over
    public void OnClick(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        if (!isPlayerTurn) return;
        if (checkingTwoCards) return;

        var rayHit = Physics2D.GetRayIntersection(camera.ScreenPointToRay(Mouse.current.position.ReadValue()));
        if (!rayHit.collider) return;

        Card card = rayHit.collider.gameObject.GetComponent<Card>();

        // Monster will remember the picture on the card that was flipped by the player
        // in its memory if this is the first time the monster is seeing this picture
        if (!cardsInMonMemory.ContainsKey(card.ID))
        {
            cardsInMonMemory.Add(card.ID, card);
            cards.Remove(card.gameObject);
        }

        card.Flip_Card(this);
    }

    // Change current game state depending on how many cards have been flipped over
    public void Change_Cards_State(Card card)
    {
        switch (cardState)
        {
            // If this is the first card flipped, wait for a second card to be flipped
            case CARDS_STATE.NO_CARD_UP:
                firstCard = card;
                cardState = CARDS_STATE.ONE_CARD_UP;
                break;
            // If this is the second card flipped, check if the two selected cards have matching pictures
            case CARDS_STATE.ONE_CARD_UP:
                secondCard = card;
                cardState = CARDS_STATE.TWO_CARD_UP;
                StartCoroutine(Check_Two_Cards());
                break;
            case CARDS_STATE.TWO_CARD_UP:
                break;
        }
    }

    // Check whether the two selected cards have matching pictures and whether points should be awarded
    IEnumerator Check_Two_Cards()
    {
        checkingTwoCards = true;
        yield return new WaitForSeconds(2);

        // Check if two selected cards have matching pictures (matching IDs)
        if (firstCard.ID == secondCard.ID)
        {
            Evaluate_Matching_Cards();
        }
        else
        {
            Reset_Two_Cards();
        }

        // Reset game state such that two cards can be selected by the opponent
        firstCard = null;
        secondCard = null;
        cardState = CARDS_STATE.NO_CARD_UP;

        isPlayerTurn = !isPlayerTurn;
        checkingTwoCards = false;

        if (!isPlayerTurn)
        {
            turnText.SetText("Lilith's Turn!");
            StartCoroutine(Monster_Move());
        }
        else
        {
            turnText.SetText("Your Turn!");
        }

        // Display winner if there no more cards left to be matched
        if (pairsLeft == 0)
        {
            if (playerMatches >= monsterMatches)
                turnText.SetText("You Won!");
            else
                turnText.SetText("Lilith Won!");
        }
    }

    // Execute this function when the two selected cards match
    private void Evaluate_Matching_Cards()
    {
        // Disable both selected cards so they cannot be reselected for the rest of the game
        firstCard.disabled = true;
        firstCard.gameObject.SetActive(false);
        if (!cardsInMonMemory.Remove(firstCard.ID))
            Debug.Log("First card not removed from monster memory");
        if (!cards.Remove(firstCard.gameObject))
            Debug.Log("First card not removed from list");

        secondCard.disabled = true;
        secondCard.gameObject.SetActive(false);
        if (!cardsInMonMemory.Remove(secondCard.ID))
            Debug.Log("Second card not removed from monster memory");
        if (!cards.Remove(secondCard.gameObject))
            Debug.Log("Second card not removed from cards available list");

        // Award points to whoever selected the matching pair of cards
        if (isPlayerTurn)
            playerMatches++;
        else
            monsterMatches++;

        monsterText.SetText("Lilith's Score: " + monsterMatches);
        playerText.SetText("Your Score: " + playerMatches);

        pairsLeft--;
        if (pairsLeft == 0)
        {
            EndGame();
        }
    }

    // Execute this function when the two selected cards do not match
    private void Reset_Two_Cards()
    {
        // Show the back side of the two cards and reset so they can be reselected
        firstCard.Reset_Card();
        secondCard.Reset_Card();
    }

    // TODO: put cards back if monster made incorrect guess
    // Simulates the monster's turn during the game
    IEnumerator Monster_Move()
    {
        yield return new WaitForSeconds(2);

        // Choose a random card for the monster to flip over from the pool of cards
        // that the monster did not save in its memory
        int index = Random.Range(0, cards.Count);
        Card chosen = cards[index].GetComponent<Card>();
        bool isCardInMonMemory = true;
        if (!cardsInMonMemory.ContainsKey(chosen.ID))
        {
            // If first time seeing a card with this picture (identified by ID), add to memory
            isCardInMonMemory = false;
            cardsInMonMemory.Add(chosen.ID, chosen);
            cards.Remove(chosen.gameObject);
        }
        chosen.Flip_Card(this);

        yield return new WaitForSeconds(2);

        if (!isCardInMonMemory)
        {
            // Pick a second random card for the monster to flip over from the pool
            // of cards that the monster did not save in its memory
            int indexTwo = Random.Range(0, cards.Count);
            Card nonMemCard = cards[indexTwo].GetComponent<Card>();

            if (!cardsInMonMemory.ContainsKey(nonMemCard.ID))
            {
                // If first time seeing a card with this picture (identified by ID), add to memory
                cardsInMonMemory.Add(nonMemCard.ID, nonMemCard);
                cards.Remove(nonMemCard.gameObject);
            }

            nonMemCard.Flip_Card(this);
        }
        else
        {
            // If monster has seen the picture of the first card before, 75% chance monster picks
            // the card with the same picture (ID) it saw before from memory
            float prob = Random.Range(0.0f, 1.0f);
            if (pairsLeft == 1 || prob < 0.75f)
            {
                // Pick card from dictionary (monster memory) for second card
                Card memCard = cardsInMonMemory[chosen.ID];
                memCard.Flip_Card(this);
            }
            else
            {
                // Pick random incorrect card from cards that are not in the monster's memory
                // for the second card
                int indexTwo = Random.Range(0, cards.Count);

                // Choose another card if picked the same as the first card
                if (indexTwo == index)
                {
                    if (indexTwo == 0)
                        indexTwo = 1;
                    else
                        indexTwo--;
                }
                
                Card nonMemCard = cards[indexTwo].GetComponent<Card>();

                if (!cardsInMonMemory.ContainsKey(nonMemCard.ID))
                {
                    // If first time seeing a card with this picture (identified by ID), add to memory
                    cardsInMonMemory.Add(nonMemCard.ID, nonMemCard);
                    cards.Remove(nonMemCard.gameObject);
                }

                nonMemCard.Flip_Card(this);
            }
        }
    }

    // Called to end the game when there are no more cards left to match
    private void EndGame()
    {
        StartCoroutine(Wait());
        if (playerMatches >= monsterMatches)
            controller.Won();
        else
            controller.Lost();
    }

    IEnumerator Wait()
    {
        yield return new WaitForSeconds(1.5f);
    }
}